namespace Ecre.Domain.Ledger;

public enum EntryDirection { Debit = 1, Credit = 2 }

public static class EntryDirectionExtensions
{
    public static EntryDirection Opposite(this EntryDirection d)
        => d == EntryDirection.Debit ? EntryDirection.Credit : EntryDirection.Debit;

    public static string Abbrev(this EntryDirection d)
        => d == EntryDirection.Debit ? "DR" : "CR";
}