namespace Ecre.Domain.Common;

public static class Rounding
{
    public const MidpointRounding Policy = MidpointRounding.AwayFromZero;

    public static decimal ToScale(decimal value, int scale)
        => Math.Round(value, scale, Policy);
}