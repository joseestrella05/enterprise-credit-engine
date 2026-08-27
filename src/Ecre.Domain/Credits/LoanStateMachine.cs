using Ecre.Domain.Common;

namespace Ecre.Domain.Credits;

public static class LoanStateMachine
{
    private static readonly IReadOnlyDictionary<LoanStatus, LoanStatus[]> Allowed =
        new Dictionary<LoanStatus, LoanStatus[]>
        {
            [LoanStatus.Draft]       = new[] { LoanStatus.UnderReview },
            [LoanStatus.UnderReview] = new[] { LoanStatus.Approved },
            [LoanStatus.Approved]    = new[] { LoanStatus.Disbursed },
            [LoanStatus.Disbursed]   = new[] { LoanStatus.Active },
            [LoanStatus.Active]      = new[] { LoanStatus.FullyPaid, LoanStatus.Defaulted },
            [LoanStatus.FullyPaid]   = Array.Empty<LoanStatus>(),
            [LoanStatus.Defaulted]   = Array.Empty<LoanStatus>(),
            [LoanStatus.Rejected]    = Array.Empty<LoanStatus>(),
            [LoanStatus.Cancelled]   = Array.Empty<LoanStatus>()
        };

    private static LoanStatus[] TargetsFrom(LoanStatus status)
        => Allowed.TryGetValue(status, out var t)
            ? t
            : throw new DomainException(
                $"Estado '{status}' no está declarado en la máquina de estados. " +
                "Toda adición a LoanStatus debe registrarse en LoanStateMachine.Allowed.");

    public static bool CanTransition(LoanStatus from, LoanStatus to)
        => TargetsFrom(from).Contains(to);

    public static bool IsTerminal(LoanStatus status)
        => TargetsFrom(status).Length == 0;

    public static void EnsureTransition(LoanStatus from, LoanStatus to)
    {
        if (CanTransition(from, to)) return;

        var targets = TargetsFrom(from);
        var permitidos = targets.Length == 0
            ? "ninguno (estado terminal)"
            : string.Join(", ", targets);

        throw new DomainException(
            $"Transición inválida: {from} → {to}. Destinos permitidos desde {from}: {permitidos}.");
    }

    public static bool AmountsAreFrozen(LoanStatus status)
        => status is not (LoanStatus.Draft or LoanStatus.UnderReview);

    /// Expuesto para diagnósticos y para la UI (qué botones habilitar).
    public static IReadOnlyCollection<LoanStatus> AllowedTargets(LoanStatus from)
        => TargetsFrom(from);
}