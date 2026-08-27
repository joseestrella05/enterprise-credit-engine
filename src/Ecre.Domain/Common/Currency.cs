namespace Ecre.Domain.Common;

public readonly record struct Currency
{
    public string Code {get;}
    public int Scale {get;}

    private Currency(string code, int scale) => (Code, Scale) = (code, scale);

    public static readonly Currency DOP = new("DOP", 2);
    public static readonly Currency USD = new("USD",2);

    public bool IsDefined => Code is not null;

    public static Currency FromCode(string code) => code?.Trim().ToUpperInvariant() switch
    {
        "DOP" => DOP,
        "USD" => USD,
        _ => throw new DomainException($"Moneda no soportada: '{code}'.")
    };

    public override string ToString() => Code ?? "<undefined>";

}