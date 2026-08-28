// src/Ecre.Domain/Credits/Loan.cs
using Ecre.Domain.Common;

namespace Ecre.Domain.Credits;

public sealed class Loan
{
    private readonly List<Installment> _installments = new();

    public Guid Id { get; private set; }
    public string Number { get; private set; } = null!;
    public Guid BorrowerId { get; private set; }
    public Money Principal { get; private set; }
    public InterestRate Rate { get; private set; }
    public Term Term { get; private set; }
    public PaymentFrequency Frequency { get; private set; }
    public LoanStatus Status { get; private set; }
    public DateOnly? DisbursedOn { get; private set; }
    public uint RowVersion { get; private set; }   // RNF-03, se cablea en Fase 4
    public string? ClosingReason { get; private set; }

    /// RF-04. Nunca nulo tras CreateDraft; el `null!` sólo cubre el ctor de EF Core.
    public LatePolicy LatePolicy { get; private set; } = null!;

    public IReadOnlyList<Installment> Installments => _installments.AsReadOnly();
    public Currency Currency => Principal.Currency;

    private Loan() { } 

    public static Loan CreateDraft(
        string number, Guid borrowerId, Money principal,
        InterestRate rate, Term term, PaymentFrequency frequency,
        LatePolicy? latePolicy = null)
    {
        if (string.IsNullOrWhiteSpace(number))
            throw new DomainException("El crédito requiere número identificador.");
        if (!principal.IsPositive)
            throw new DomainException($"Capital inválido: {principal}.");

        return new Loan
        {
            Id = Guid.NewGuid(),
            Number = number.Trim(),
            BorrowerId = borrowerId,
            Principal = principal,
            Rate = rate,
            Term = term,
            Frequency = frequency,
            Status = LoanStatus.Draft,
            // Sin política explícita, la mora corre a la misma tasa que la corriente.
            LatePolicy = latePolicy ?? Credits.LatePolicy.Default(rate)
        };
    }

    public void Amend(Money? principal = null, InterestRate? rate = null, Term? term = null)
    {
        if (LoanStateMachine.AmountsAreFrozen(Status))
            throw new DomainException(
                $"Los términos económicos están congelados en estado {Status}. " +
                "Use una reestructuración formal.");

        if (principal is { } p)
        {
            if (!p.IsPositive) throw new DomainException($"Capital inválido: {p}.");
            Principal = p;
        }
        if (rate is { } r) Rate = r;
        if (term is { } t) Term = t;
    }

    public void SetLatePolicy(LatePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        LatePolicy = policy;
    }

    public void SubmitForReview() => TransitionTo(LoanStatus.UnderReview);

    public void Approve() => TransitionTo(LoanStatus.Approved);

    public void Disburse(DateOnly disbursedOn, DateOnly firstDueDate)
    {
        TransitionTo(LoanStatus.Disbursed);

        if (firstDueDate <= disbursedOn)
            throw new DomainException(
                $"El primer vencimiento ({firstDueDate}) debe ser posterior al desembolso ({disbursedOn}).");

        DisbursedOn = disbursedOn;

        var schedule = FrenchAmortization.Build(Principal, Rate, Term, Frequency, firstDueDate);

        _installments.Clear();
        _installments.AddRange(schedule.Select(s => new Installment(s, Currency)));
    }

    public void Activate()
    {
        if (_installments.Count == 0)
            throw new DomainException("No se puede activar un crédito sin tabla de amortización.");

        TransitionTo(LoanStatus.Active);
    }

    public void MarkFullyPaid() => TransitionTo(LoanStatus.FullyPaid);
    public void MarkDefaulted() => TransitionTo(LoanStatus.Defaulted);

    private void TransitionTo(LoanStatus target)
    {
        LoanStateMachine.EnsureTransition(Status, target);
        Status = target;
    }

    public void Reject(string reason)
    {
        RequireReason(reason, nameof(Reject));
        TransitionTo(LoanStatus.Rejected);
        ClosingReason = reason.Trim();
    }   


    public void Cancel(string reason)
    {
        RequireReason(reason, nameof(Cancel));
        TransitionTo(LoanStatus.Cancelled);
        ClosingReason = reason.Trim();
    }

    private static void RequireReason(string reason, string operation)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException($"La operación {operation} exige una justificación registrada.");
    }

    // ======================================================================
    // Fase 3 — motor de pagos, mora y prepagos
    // ======================================================================

    /// Capital vivo del crédito: suma del capital pendiente de todas las cuotas.
    public Money OutstandingPrincipal => Sum(x => x.OutstandingPrincipal);

    /// Interés corriente devengado y no pagado.
    public Money OutstandingInterest => Sum(x => x.OutstandingInterest);

    /// Mora devengada y no pagada.
    public Money OutstandingLateInterest => Sum(x => x.OutstandingLateInterest);

    /// Deuda total exigible al día de hoy con lo ya devengado.
    public Money TotalOutstanding => Sum(x => x.OutstandingTotal);

    /// RF-04: capital de las cuotas cuya gracia ya expiró. Es la ÚNICA base
    /// sobre la que puede devengarse mora; nunca el saldo total del crédito.
    public Money OverduePrincipal(DateOnly asOf)
        => _installments
            .Where(x => LatePolicy.IsOverdue(x.DueDate, asOf))
            .Aggregate(Money.Zero(Currency), (a, x) => a + x.OutstandingPrincipal);

    /// Devenga mora en todas las cuotas hasta `asOf`. Idempotente por fecha.
    public Money AccrueLateInterest(DateOnly asOf)
    {
        RequireActive();

        var total = Money.Zero(Currency);
        foreach (var installment in _installments)
            total += installment.AccrueLateInterest(LatePolicy, asOf);

        return total;
    }

    /// RF-03: aplica un pago ordinario. Devenga mora ANTES de imputar, para no
    /// cobrar capital por delante de una mora ya causada.
    public PaymentResult ApplyPayment(Money amount, DateOnly paidOn)
    {
        RequireActive();
        RequirePayable(amount);

        AccrueLateInterest(paidOn);

        var log = new List<InstallmentAllocation>();
        var total = AbsorbAcross(Pending(), amount, log);

        return new PaymentResult(amount, paidOn, total, log, SettleIfFullyPaid());
    }

    /// RF-03: pago extraordinario. Primero salda todo lo vencido; sólo el
    /// remanente abona a capital y dispara el recálculo de la tabla.
    public PrepaymentResult ApplyPrepayment(Money amount, DateOnly paidOn, PrepaymentMode mode)
    {
        RequireActive();
        RequirePayable(amount);

        AccrueLateInterest(paidOn);

        // 1) Lo vencido primero: toda cuota con vencimiento ya cumplido.
        var arrears = AbsorbAcross(Pending().Where(x => x.DueDate <= paidOn), amount, null);
        var toPrincipal = arrears.Remainder;

        // 2) El remanente abona a capital de las cuotas futuras intactas.
        var future = Rewritable(paidOn);
        var previousCount = future.Count;
        var previousInstallment = previousCount == 0 ? Money.Zero(Currency) : future[0].ScheduledTotal;

        var futurePrincipal = future.Aggregate(Money.Zero(Currency), (a, x) => a + x.OutstandingPrincipal);
        var prepaid = Money.Min(toPrincipal, futurePrincipal);
        var unapplied = toPrincipal - prepaid;

        if (prepaid.IsPositive)
            Recalculate(future, futurePrincipal - prepaid, previousInstallment, mode);

        var remaining = _installments.Where(x => !x.IsSettled && x.DueDate > paidOn).ToList();

        return new PrepaymentResult(
            Amount: amount,
            PaidOn: paidOn,
            Mode: mode,
            Arrears: arrears,
            PrincipalPrepaid: prepaid,
            Unapplied: unapplied,
            PreviousRemainingInstallments: previousCount,
            NewRemainingInstallments: remaining.Count,
            PreviousInstallmentAmount: previousInstallment,
            NewInstallmentAmount: remaining.Count == 0 ? Money.Zero(Currency) : remaining[0].ScheduledTotal,
            LoanFullyPaid: SettleIfFullyPaid());
    }

    // ---------------------------------------------------------------- internos

    /// Prelación entre cuotas: siempre la más antigua no saldada primero.
    private IEnumerable<Installment> Pending()
        => _installments.OrderBy(x => x.Number).Where(x => !x.IsSettled);

    private PaymentSplit AbsorbAcross(
        IEnumerable<Installment> targets, Money available, List<InstallmentAllocation>? log)
    {
        var principal = Money.Zero(Currency);
        var interest = Money.Zero(Currency);
        var late = Money.Zero(Currency);

        foreach (var installment in targets.OrderBy(x => x.Number))
        {
            if (!available.IsPositive) break;

            var split = installment.Absorb(available);
            if (!split.Applied.IsPositive) continue;

            principal += split.Principal;
            interest  += split.Interest;
            late      += split.LateInterest;
            available  = split.Remainder;

            log?.Add(new InstallmentAllocation(installment.Number, split));
        }

        return new PaymentSplit(principal, interest, late, available);
    }

    /// Cuotas futuras que el recálculo puede reescribir: vencen después del pago
    /// y no tienen imputación alguna. Las ya pagadas (total o parcialmente) se
    /// preservan intactas.
    private List<Installment> Rewritable(DateOnly paidOn)
        => _installments
            .Where(x => x.DueDate > paidOn && !x.IsSettled && !x.HasPayments)
            .OrderBy(x => x.Number)
            .ToList();

    private void Recalculate(
        List<Installment> future, Money newBalance, Money currentInstallment, PrepaymentMode mode)
    {
        var i = Rate.PeriodicNominal(Frequency);

        if (!newBalance.IsPositive)
        {
            // El prepago liquidó todo el capital futuro: cuotas a cero y fuera.
            foreach (var installment in future)
                installment.Rewrite(Money.Zero(Currency), Money.Zero(Currency));

            _installments.RemoveAll(future.Contains);
            return;
        }

        var periods = mode switch
        {
            PrepaymentMode.ReduceInstallment => future.Count,
            PrepaymentMode.ReduceTerm        => Math.Min(future.Count, SolveTerm(newBalance, i, currentInstallment)),
            _ => throw new DomainException($"Modalidad de prepago no soportada: {mode}.")
        };

        var tail = mode == PrepaymentMode.ReduceTerm
            ? LevelPaymentTail(newBalance, i, currentInstallment, periods)
            : FrenchAmortization
                .Build(newBalance, Rate, Term.Of(periods), Frequency, future[0].DueDate)
                .Select(s => (s.Principal, s.Interest))
                .ToList();

        for (int k = 0; k < periods; k++)
            future[k].Rewrite(tail[k].Principal, tail[k].Interest);

        // ReduceTerm puede dejar cuotas sobrantes al final: se eliminan.
        for (int k = periods; k < future.Count; k++)
            _installments.Remove(future[k]);
    }

    /// Despeja n de la fórmula francesa: n = log(A / (A − i·P)) / log(1+i).
    private static int SolveTerm(Money balance, decimal i, Money payment)
    {
        if (!payment.IsPositive)
            throw new DomainException("No se puede recalcular el plazo sin cuota de referencia.");

        if (i == 0m)
            return Math.Max(1, (int)Math.Ceiling(balance.Amount / payment.Amount));

        var periodicInterest = i * balance.Amount;
        if (payment.Amount <= periodicInterest)
            throw new DomainException(
                $"La cuota {payment} no cubre el interés periódico del saldo {balance}; el plazo no converge.");

        var n = Math.Log((double)(payment.Amount / (payment.Amount - periodicInterest)))
              / Math.Log(1d + (double)i);

        return Math.Max(1, (int)Math.Ceiling(n - 1e-9));
    }

    /// Tabla de cuota constante `payment` sobre `balance`; la última absorbe el
    /// residuo, que es lo que hace que el plazo se acorte sin mover la cuota.
    private List<(Money Principal, Money Interest)> LevelPaymentTail(
        Money balance, decimal i, Money payment, int periods)
    {
        var rows = new List<(Money, Money)>(periods);

        for (int k = 1; k <= periods; k++)
        {
            var interest = i == 0m ? Money.Zero(Currency) : Money.Of(balance.Amount * i, Currency);

            var principal = k == periods
                ? balance
                : payment - interest;

            if (!principal.IsPositive)
                throw new DomainException(
                    $"Recálculo no amortizable en el período {k}: la cuota {payment} no cubre {interest}.");

            balance -= principal;
            rows.Add((principal, interest));
        }

        return rows;
    }

    private bool SettleIfFullyPaid()
    {
        if (_installments.Count > 0 && _installments.Any(x => !x.IsSettled))
            return false;

        MarkFullyPaid();
        return true;
    }

    private Money Sum(Func<Installment, Money> selector)
        => _installments.Aggregate(Money.Zero(Currency), (a, x) => a + selector(x));

    private void RequireActive()
    {
        if (Status != LoanStatus.Active)
            throw new DomainException(
                $"Sólo un crédito Active admite movimientos de pago. Estado actual: {Status}.");
    }

    private void RequirePayable(Money amount)
    {
        if (!amount.Currency.Equals(Currency))
            throw new DomainException(
                $"El pago está en {amount.Currency} y el crédito en {Currency}.");
        if (!amount.IsPositive)
            throw new DomainException($"El monto del pago debe ser positivo. Recibido: {amount}.");
    }
}
