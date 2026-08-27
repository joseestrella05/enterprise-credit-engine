namespace Ecre.Domain.Common;

public readonly record struct Term
{
    public int Installments { get; }

    private Term(int installments) => Installments = installments;

    public static Term Of(int installments)
    {
        if (installments <= 0)
            throw new DomainException("El plazo debe ser de al menos 1 cuota.");
        if (installments > 600)
            throw new DomainException($"Plazo excesivo: {installments} cuotas.");

        return new Term(installments);
    }

    public override string ToString() => $"{Installments} cuotas";
}