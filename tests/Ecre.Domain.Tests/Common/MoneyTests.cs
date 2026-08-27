using Ecre.Domain.Common;
using FluentAssertions;

namespace Ecre.Domain.Tests.Common;

public class MoneyTests
{
    [Fact]
    public void Of_AplicaRedondeoComercial_NoBankers()
    {
        Money.Of(2.005m, Currency.DOP).Amount.Should().Be(2.01m);
        Money.Of(2.015m, Currency.DOP).Amount.Should().Be(2.02m);
    }

    [Fact]
    public void Operaciones_EntreMonedasDistintas_Fallan()
    {
        var accion = () => Money.Of(100m, Currency.DOP) + Money.Of(100m, Currency.USD);
        accion.Should().Throw<DomainException>();
    }

    [Fact]
    public void Pow_MantienePrecisionDecimal()
    {
        DecimalMath.Pow(1.02m, 3).Should().Be(1.061208m);
    }
}