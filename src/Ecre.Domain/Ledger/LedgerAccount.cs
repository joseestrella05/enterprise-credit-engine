namespace Ecre.Domain.Ledger;

public sealed record LedgerAccount
{
    public string Code { get; }
    public string Name { get; }
    public AccountType Type { get; }

    public bool IsContra { get; }

    internal LedgerAccount(string code, string name, AccountType type, bool isContra = false)
        => (Code, Name, Type, IsContra) = (code, name, type, isContra);

    public EntryDirection IncreasesWith
        => IsContra ? Type.NaturalBalance().Opposite() : Type.NaturalBalance();

    public override string ToString() => $"{Code} {Name}";
}