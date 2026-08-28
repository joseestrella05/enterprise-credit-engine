// src/Ecre.Domain/Credits/Installment.cs  — reemplaza el de la Fase 1
using Ecre.Domain.Common;

namespace Ecre.Domain.Credits;

public enum InstallmentStatus { Pending = 0, PartiallyPaid = 1, Paid = 2, Overdue = 3 }

public sealed class Installment
{
    public Guid Id { get; private set; }
    public int Number { get; private set; }
    public DateOnly DueDate { get; private set; }

    public Money ScheduledPrincipal { get; private set; }
    public Money ScheduledInterest { get; private set; }

    public Money PaidPrincipal { get; private set; }
    public Money PaidInterest { get; private set; }

    /// Mora devengada y aún no pagada. Se acumula por el proceso de cierre diario.
    public Money AccruedLateInterest { get; private set; }
    public Money PaidLateInterest { get; private set; }
    public DateOnly? LateAccruedThrough { get; private set; }

    public InstallmentStatus Status { get; private set; }

    private Installment() { } // EF Core

    internal Installment(ScheduledInstallment s, Currency currency)
    {
        Id = Guid.NewGuid();
        Number = s.Number;
        DueDate = s.DueDate;
        ScheduledPrincipal = s.Principal;
        ScheduledInterest = s.Interest;
        PaidPrincipal = Money.Zero(currency);
        PaidInterest = Money.Zero(currency);
        AccruedLateInterest = Money.Zero(currency);
        PaidLateInterest = Money.Zero(currency);
        Status = InstallmentStatus.Pending;
    }

    public Currency Currency => ScheduledPrincipal.Currency;

    public Money OutstandingPrincipal    => ScheduledPrincipal - PaidPrincipal;
    public Money OutstandingInterest     => ScheduledInterest - PaidInterest;
    public Money OutstandingLateInterest => AccruedLateInterest - PaidLateInterest;
    public Money OutstandingTotal
        => OutstandingPrincipal + OutstandingInterest + OutstandingLateInterest;

    public bool IsSettled => OutstandingTotal.IsZero;

    /// Cuota contractual (capital + interés corriente), sin mora.
    public Money ScheduledTotal => ScheduledPrincipal + ScheduledInterest;

    /// True si ya recibió alguna imputación; el recálculo por prepago la respeta.
    public bool HasPayments
        => PaidPrincipal.IsPositive || PaidInterest.IsPositive || PaidLateInterest.IsPositive;

    /// RF-04: devenga mora sobre el capital vencido de ESTA cuota.
    /// Idempotente por fecha: llamarlo dos veces el mismo día no duplica.
    internal Money AccrueLateInterest(LatePolicy policy, DateOnly asOf)
    {
        if (Status == InstallmentStatus.Paid || !OutstandingPrincipal.IsPositive)
            return Money.Zero(Currency);

        var from = LateAccruedThrough ?? DueDate.AddDays(policy.GraceDays);
        if (asOf <= from) return Money.Zero(Currency);

        var days = asOf.DayNumber - from.DayNumber;
        var delta = Money.Of(OutstandingPrincipal.Amount * policy.DailyRate * days, Currency);

        AccruedLateInterest += delta;
        LateAccruedThrough = asOf;

        if (policy.IsOverdue(DueDate, asOf)) Status = InstallmentStatus.Overdue;

        return delta;
    }

    /// Absorbe del pago disponible en el orden del RF-03 y devuelve el remanente.
    internal PaymentSplit Absorb(Money available)
    {
        var toLate      = Money.Min(available, OutstandingLateInterest);
        available      -= toLate;
        var toInterest  = Money.Min(available, OutstandingInterest);
        available      -= toInterest;
        var toPrincipal = Money.Min(available, OutstandingPrincipal);
        available      -= toPrincipal;

        PaidLateInterest += toLate;
        PaidInterest     += toInterest;
        PaidPrincipal    += toPrincipal;

        RefreshStatus();

        return new PaymentSplit(toPrincipal, toInterest, toLate, available);
    }

    private void RefreshStatus()
    {
        if (IsSettled)
        {
            Status = InstallmentStatus.Paid;
            return;
        }

        bool algoPagado = PaidPrincipal.IsPositive || PaidInterest.IsPositive || PaidLateInterest.IsPositive;

        if (Status != InstallmentStatus.Overdue)
            Status = algoPagado ? InstallmentStatus.PartiallyPaid : InstallmentStatus.Pending;
    }

    /// Usado por el recálculo tras pago extraordinario. Sólo se invoca sobre
    /// cuotas futuras sin imputaciones previas, para no falsear lo ya pagado.
    internal void Rewrite(Money principal, Money interest)
    {
        if (principal.IsNegative || interest.IsNegative)
            throw new DomainException(
                $"Recálculo inválido para la cuota {Number}: capital {principal}, interés {interest}.");

        ScheduledPrincipal = principal;
        ScheduledInterest = interest;
        RefreshStatus();
    }
}