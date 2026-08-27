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

    public IReadOnlyList<Installment> Installments => _installments.AsReadOnly();
    public Currency Currency => Principal.Currency;

    private Loan() { } 

    public static Loan CreateDraft(
        string number, Guid borrowerId, Money principal,
        InterestRate rate, Term term, PaymentFrequency frequency)
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
            Status = LoanStatus.Draft
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
}