namespace Ecre.Domain.Common;

public readonly record struct InterestRate
{
    public decimal AnnualNominal { get; }

    private InterestRate(decimal annualNominal) => AnnualNominal = annualNominal;

    public static InterestRate FromAnnualPercentage(decimal percentage)
    {
        if (percentage < 0m)
            throw new DomainException("La tasa no puede ser negativa.");
        if (percentage > 1000m)
            throw new DomainException($"Tasa fuera de rango razonable: {percentage}%.");

        return new InterestRate(percentage / 100m);
    }

    /// Atajo para el caso local típico: "2.5% mensual".
    public static InterestRate FromMonthlyPercentage(decimal percentage)
        => FromAnnualPercentage(percentage * 12m);

    public static readonly InterestRate ZeroRate = new(0m);

    public decimal PeriodicNominal(PaymentFrequency frequency)
        => AnnualNominal / (int)frequency;

    public decimal PeriodicEffective(PaymentFrequency frequency)
        => (decimal)(Math.Pow(1d + (double)AnnualNominal, 1d / (int)frequency) - 1d);

    public decimal AsPercentage => AnnualNominal * 100m;
    public override string ToString() => $"{AsPercentage:0.####}% anual";
}