using Ecre.Domain.Common;

namespace Ecre.Domain.Credits;

/// Resultado de un pago extraordinario: primero se salda lo vencido (`Arrears`)
/// y sólo el remanente abona a capital (`PrincipalPrepaid`).
public sealed record PrepaymentResult(
    Money Amount,
    DateOnly PaidOn,
    PrepaymentMode Mode,
    PaymentSplit Arrears,
    Money PrincipalPrepaid,
    Money Unapplied,
    int PreviousRemainingInstallments,
    int NewRemainingInstallments,
    Money PreviousInstallmentAmount,
    Money NewInstallmentAmount,
    bool LoanFullyPaid)
{
    public int InstallmentsSaved => PreviousRemainingInstallments - NewRemainingInstallments;
}
