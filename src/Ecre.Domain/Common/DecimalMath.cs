namespace Ecre.Domain.Common;

public static class DecimalMath
{
    public static decimal Pow(decimal @base, int exponent)
    {
        if (exponent < 0)
            throw new DomainException("Exponente negativo no soportado.");

        decimal result = 1m;
        decimal factor = @base;
        int e = exponent;

        while (e > 0)
        {
            if ((e & 1) == 1) result *= factor;
            e >>= 1;
            if (e > 0) factor *= factor;
        }

        return result;
    }
}