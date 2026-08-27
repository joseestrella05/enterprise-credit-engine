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
            [LoanStatus.Defaulted]   = Array.Empty<LoanStatus>()
        };

    public static bool CanTransition(LoanStatus from, LoanStatus to)
        => Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static bool IsTerminal(LoanStatus status)
        => Allowed[status].Length == 0;

    public static void EnsureTransition(LoanStatus from, LoanStatus to)
    {
        if (CanTransition(from, to)) return;

        var permitidos = Allowed[from].Length == 0
            ? "ninguno (estado terminal)"
            : string.Join(", ", Allowed[from]);

        throw new DomainException(
            $"Transición inválida: {from} → {to}. Destinos permitidos desde {from}: {permitidos}.");
    }

    public static bool AmountsAreFrozen(LoanStatus status)
        => status >= LoanStatus.Approved;
}