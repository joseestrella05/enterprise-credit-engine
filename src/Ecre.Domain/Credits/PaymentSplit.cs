using Ecre.Domain.Common;

namespace Ecre.Domain.Credits;

public readonly record struct PaymentSplit(
    Money Principal, Money Interest, Money LateInterest, Money Remainder)
{
    public Money Applied => Principal + Interest + LateInterest;
}