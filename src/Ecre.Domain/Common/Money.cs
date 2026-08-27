namespace Ecre.Domain.Common;

public readonly record struct Money : IComparable<Money>
{
    public decimal Amount { get; }
    public Currency Currency { get; }

    private Money(decimal amount, Currency currency)
        => (Amount, Currency) = (amount, currency);

    /// Única puerta de entrada: redondea a la escala de la moneda.
    public static Money Of(decimal amount, Currency currency)
    {
        if (!currency.IsDefined)
            throw new DomainException("No se puede construir Money sin moneda definida.");

        return new Money(Rounding.ToScale(amount, currency.Scale), currency);
    }

    public static Money Zero(Currency currency) => Of(0m, currency);

    public bool IsZero     => Amount == 0m;
    public bool IsPositive => Amount > 0m;
    public bool IsNegative => Amount < 0m;

    public static Money operator +(Money a, Money b)
        => new(a.Amount + b.Amount, SameCurrency(a, b));

    public static Money operator -(Money a, Money b)
        => new(a.Amount - b.Amount, SameCurrency(a, b));

    public static Money operator -(Money a) => new(-a.Amount, a.Currency);

    /// Multiplicación por escalar: SÍ redondea. Para cálculos encadenados
    /// (amortización) trabaja con Amount crudo y materializa al final.
    public static Money operator *(Money a, decimal factor)
        => Of(a.Amount * factor, a.Currency);

    public static bool operator >(Money a, Money b) => a.CompareTo(b) > 0;
    public static bool operator <(Money a, Money b) => a.CompareTo(b) < 0;
    public static bool operator >=(Money a, Money b) => a.CompareTo(b) >= 0;
    public static bool operator <=(Money a, Money b) => a.CompareTo(b) <= 0;

    public static Money Min(Money a, Money b) => a <= b ? a : b;
    public static Money Max(Money a, Money b) => a >= b ? a : b;

    public int CompareTo(Money other)
    {
        SameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

    private static Currency SameCurrency(Money a, Money b)
    {
        if (!a.Currency.Equals(b.Currency))
            throw new DomainException(
                $"Operación entre monedas distintas: {a.Currency} y {b.Currency}.");
        return a.Currency;
    }

    public override string ToString()
        => $"{Amount.ToString($"N{Currency.Scale}")} {Currency}";
}