namespace Ecre.Domain.Credits;

/// RF-03: modalidades de aplicación de un pago extraordinario a capital.
public enum PrepaymentMode
{
    /// Misma cuota, menos períodos.
    ReduceTerm = 0,

    /// Mismo plazo, cuota menor.
    ReduceInstallment = 1
}
