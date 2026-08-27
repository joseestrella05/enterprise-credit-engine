using Ecre.Domain.Common;

namespace Ecre.Domain.Ledger;

public sealed class JournalEntry
{
    public Guid Id { get; private set; }
    public string AccountCode { get; private set; } = null!;
    public EntryDirection Direction { get; private set; }
    public Money Amount { get; private set; }
    public string? Memo { get; private set; }

    private JournalEntry() { } // EF Core

    private JournalEntry(string accountCode, EntryDirection direction, Money amount, string? memo)
    {
        Id = Guid.NewGuid();
        AccountCode = accountCode;
        Direction = direction;
        Amount = amount;
        Memo = memo;
    }

    public static JournalEntry Debit(LedgerAccount account, Money amount, string? memo = null)
        => Create(account, EntryDirection.Debit, amount, memo);

    public static JournalEntry Credit(LedgerAccount account, Money amount, string? memo = null)
        => Create(account, EntryDirection.Credit, amount, memo);

    private static JournalEntry Create(LedgerAccount account, EntryDirection dir, Money amount, string? memo)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (!amount.IsPositive)
            throw new DomainException(
                $"Un asiento requiere monto estrictamente positivo. Recibido {amount} en {account}. " +
                "Para invertir el efecto use la dirección contraria, no un monto negativo.");

        return new JournalEntry(account.Code, dir, amount, memo);
    }

    public LedgerAccount Account => ChartOfAccounts.FromCode(AccountCode);

    internal JournalEntry Mirror()
        => new(AccountCode, Direction.Opposite(), Amount, $"Reversión: {Memo ?? AccountCode}");

    public override string ToString() => $"{Direction.Abbrev()} {Account} {Amount}";
}