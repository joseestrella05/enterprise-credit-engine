using Ecre.Domain.Common;
using Ecre.Domain.Credits;
using FluentAssertions;

namespace Ecre.Domain.Tests.Credits;

public class FrenchAmortizationTests
{
    private static readonly DateOnly FirstDue = new(2026, 1, 31);

    private static IReadOnlyList<ScheduledInstallment> Build(
        decimal principal, decimal annualPct, int n)
        => FrenchAmortization.Build(
            Money.Of(principal, Currency.DOP),
            InterestRate.FromAnnualPercentage(annualPct),
            Term.Of(n),
            PaymentFrequency.Monthly,
            FirstDue);

    [Theory]
    [InlineData(100_000, 24, 12)]
    [InlineData(1_000_000, 18.75, 60)]
    [InlineData(7_333.33, 36, 7)]
    [InlineData(500, 0, 3)]         
    [InlineData(1_000, 30, 1)]       
    public void SumaDeCapitales_IgualaExactamenteElPrincipal(decimal p, decimal r, int n)
    {
        var tabla = Build(p, r, n);

        tabla.Sum(x => x.Principal.Amount).Should().Be(Money.Of(p, Currency.DOP).Amount);
        tabla[^1].EndingBalance.IsZero.Should().BeTrue();
    }

    [Fact]
    public void CuotaFija_CoincideConLaFormula()
    {
        var tabla = Build(100_000m, 24m, 12);

        tabla[0].Total.Amount.Should().Be(9_455.96m);
        tabla[0].Interest.Amount.Should().Be(2_000.00m);
        tabla[0].Principal.Amount.Should().Be(7_455.96m);
    }

    [Fact]
    public void TasaCero_DistribuyeCapitalUniformemente()
    {
        var tabla = Build(500m, 0m, 3);

        tabla.Should().OnlyContain(x => x.Interest.IsZero);
        tabla[0].Principal.Amount.Should().Be(166.67m);
        tabla[2].Principal.Amount.Should().Be(166.66m); 
    }

    [Fact]
    public void FechasMensuales_NoPierdenElDia31()
    {
        var tabla = Build(100_000m, 24m, 4);

        tabla[0].DueDate.Should().Be(new DateOnly(2026, 1, 31));
        tabla[1].DueDate.Should().Be(new DateOnly(2026, 2, 28)); 
        tabla[3].DueDate.Should().Be(new DateOnly(2026, 4, 30));
        tabla[2].DueDate.Should().Be(new DateOnly(2026, 3, 31)); 
    }
}