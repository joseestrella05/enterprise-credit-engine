using Ecre.Domain.Common;

namespace Ecre.Domain.Credits;

/// Resultado de aplicar un pago ordinario. `Total.Remainder` es el excedente
/// que NO se reconoce como ingreso (decisión de diseño: se devuelve al cliente).
public sealed record PaymentResult(
    Money Amount,
    DateOnly PaidOn,
    PaymentSplit Total,
    IReadOnlyList<InstallmentAllocation> Allocations,
    bool LoanFullyPaid)
{
    public Money Applied        => Total.Applied;
    public Money Unapplied      => Total.Remainder;
    public Money Principal      => Total.Principal;
    public Money Interest       => Total.Interest;
    public Money LateInterest   => Total.LateInterest;
}
