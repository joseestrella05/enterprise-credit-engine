using Ecre.Domain.Common;

namespace Ecre.Domain.Ledger;

public enum AccountType { Asset, Liability, Equity, Income, Expense }

public static class AccountTypeExtensions
{
    /// Naturaleza del saldo: hacia qué lado aumenta la cuenta.
    public static EntryDirection NaturalBalance(this AccountType type) => type switch
    {
        AccountType.Asset or AccountType.Expense => EntryDirection.Debit,
        AccountType.Liability or AccountType.Equity or AccountType.Income => EntryDirection.Credit,
        _ => throw new DomainException($"Tipo de cuenta desconocido: {type}.")
    };
}