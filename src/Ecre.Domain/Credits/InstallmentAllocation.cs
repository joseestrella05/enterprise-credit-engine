using Ecre.Domain.Common;

namespace Ecre.Domain.Credits;

/// Trazabilidad de a qué cuota fue a parar cada porción de un pago.
public readonly record struct InstallmentAllocation(int InstallmentNumber, PaymentSplit Split)
{
    public Money Applied => Split.Applied;
}
