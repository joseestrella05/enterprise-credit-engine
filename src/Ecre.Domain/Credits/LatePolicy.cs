using Ecre.Domain.Common;

namespace Ecre.Domain.Credits;

public enum DayCountBasis { Actual360 = 360, Actual365 = 365 }

public sealed record LatePolicy
{
    public int GraceDays { get; }
    public InterestRate AnnualLateRate { get; }
    public DayCountBasis Basis { get; }

    public LatePolicy(int graceDays, InterestRate annualLateRate, DayCountBasis basis = DayCountBasis.Actual360)
    {
        if (graceDays < 0)
            throw new DomainException("Los días de gracia no pueden ser negativos.");

        GraceDays = graceDays;
        AnnualLateRate = annualLateRate;
        Basis = basis;
    }

    /// RF-04 por defecto: 3 días de gracia.
    public static LatePolicy Default(InterestRate annualLateRate)
        => new(graceDays: 3, annualLateRate, DayCountBasis.Actual360);

    public decimal DailyRate => AnnualLateRate.AnnualNominal / (int)Basis;

    public int LateDays(DateOnly dueDate, DateOnly asOf)
    {
        var graceEnd = dueDate.AddDays(GraceDays);
        var days = asOf.DayNumber - graceEnd.DayNumber;
        return days > 0 ? days : 0;
    }

    public bool IsOverdue(DateOnly dueDate, DateOnly asOf) => LateDays(dueDate, asOf) > 0;

    public Money Accrue(Money overduePrincipal, DateOnly dueDate, DateOnly asOf)
    {
        var days = LateDays(dueDate, asOf);
        if (days == 0 || !overduePrincipal.IsPositive)
            return Money.Zero(overduePrincipal.Currency);

        return Money.Of(overduePrincipal.Amount * DailyRate * days, overduePrincipal.Currency);
    }
}