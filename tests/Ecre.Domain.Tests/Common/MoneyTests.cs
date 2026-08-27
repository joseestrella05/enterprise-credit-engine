namespace Ecre.Domain.Tests.Common;

using Ecre.Domain.Common;
using FluentAssertions;
using Xunit;

public class MoneyTests
{
    [Fact]
    public void Of_WithUndefinedCurrency_ShouldThrowDomainException()
    {
        var action = () => Money.Of(100m, default);
        action.Should().Throw<DomainException>()
              .WithMessage("*sin moneda definida*");
    }

    [Theory]
    [InlineData(10.555, 10.56)] // Redondeo comercial: 5 hacia arriba
    [InlineData(10.554, 10.55)]
    [InlineData(2.505, 2.51)]
    public void Of_ShouldApplyCommercialRounding(decimal input, decimal expected)
    {
        var money = Money.Of(input, Currency.DOP);
        money.Amount.Should().Be(expected);
    }

    [Fact]
    public void Addition_WithDifferentCurrencies_ShouldThrowDomainException()
    {
        var dop = Money.Of(100m, Currency.DOP);
        var usd = Money.Of(100m, Currency.USD);

        var action = () => { var _ = dop + usd; };
        action.Should().Throw<DomainException>()
              .WithMessage("*monedas distintas*");
    }

    [Fact]
    public void Min_ShouldReturnLesserAmount()
    {
        var m1 = Money.Of(50m, Currency.DOP);
        var m2 = Money.Of(100m, Currency.DOP);

        Money.Min(m1, m2).Should().Be(m1);
    }
}
