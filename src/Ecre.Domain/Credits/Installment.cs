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
    public InstallmentStatus Status { get; private set; }

    private Installment() { } 

    internal Installment(ScheduledInstallment s, Currency currency)
    {
        Id = Guid.NewGuid();
        Number = s.Number;
        DueDate = s.DueDate;
        ScheduledPrincipal = s.Principal;
        ScheduledInterest = s.Interest;
        PaidPrincipal = Money.Zero(currency);
        PaidInterest = Money.Zero(currency);
        Status = InstallmentStatus.Pending;
    }

    public Money ScheduledTotal    => ScheduledPrincipal + ScheduledInterest;
    public Money OutstandingPrincipal => ScheduledPrincipal - PaidPrincipal;
    public Money OutstandingInterest  => ScheduledInterest  - PaidInterest;
    public Money OutstandingTotal     => OutstandingPrincipal + OutstandingInterest;

}