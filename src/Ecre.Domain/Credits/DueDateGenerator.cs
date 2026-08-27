using Ecre.Domain.Common;

namespace Ecre.Domain.Credits;

public static class DueDateGenerator
{
    public static DateOnly For(DateOnly anchor, PaymentFrequency frequency, int index)
        => frequency switch
        {
            PaymentFrequency.Monthly     => anchor.AddMonths(index),
            PaymentFrequency.Quarterly   => anchor.AddMonths(index * 3),
            PaymentFrequency.Biweekly    => anchor.AddDays(index * 14),
            PaymentFrequency.Weekly      => anchor.AddDays(index * 7),
            PaymentFrequency.Semimonthly => Semimonthly(anchor, index),
            _ => throw new DomainException($"Frecuencia no soportada: {frequency}.")
        };

    private static DateOnly Semimonthly(DateOnly anchor, int index)
    {
        bool anchorIsMidMonth = anchor.Day <= 15;
        int totalHalves = (anchorIsMidMonth ? 0 : 1) + index;
        var target = anchor.AddMonths(totalHalves / 2);

        return totalHalves % 2 == 0
            ? new DateOnly(target.Year, target.Month, 15)
            : new DateOnly(target.Year, target.Month, DateTime.DaysInMonth(target.Year, target.Month));
    }
}