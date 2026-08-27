using Ecre.Domain.Common;

namespace Ecre.Domain.Ledger;

public static class ChartOfAccounts
{
    // --- Activo ---
    public static readonly LedgerAccount Bank =
        new("1101", "Bancos", AccountType.Asset);
    public static readonly LedgerAccount Cash =
        new("1102", "Caja", AccountType.Asset);
    public static readonly LedgerAccount LoanPortfolio =
        new("1301", "Cartera de Préstamos - Capital", AccountType.Asset);
    public static readonly LedgerAccount InterestReceivable =
        new("1302", "Intereses por Cobrar", AccountType.Asset);
    public static readonly LedgerAccount LateInterestReceivable =
        new("1303", "Intereses Moratorios por Cobrar", AccountType.Asset);
    public static readonly LedgerAccount LoanLossAllowance =
        new("1390", "Provisión para Cartera Incobrable", AccountType.Asset, isContra: true);

    // --- Pasivo ---
    public static readonly LedgerAccount UnappliedPayments =
        new("2201", "Pagos Recibidos por Aplicar", AccountType.Liability);

    // --- Ingresos ---
    public static readonly LedgerAccount InterestIncome =
        new("4101", "Ingresos por Intereses Corrientes", AccountType.Income);
    public static readonly LedgerAccount LateInterestIncome =
        new("4102", "Ingresos por Intereses Moratorios", AccountType.Income);
    public static readonly LedgerAccount FeeIncome =
        new("4201", "Ingresos por Comisiones", AccountType.Income);

    // --- Gasto ---
    public static readonly LedgerAccount LoanLossExpense =
        new("5101", "Gasto por Provisión de Cartera", AccountType.Expense);
    public static readonly LedgerAccount WriteOffExpense =
        new("5102", "Castigo de Cartera", AccountType.Expense);

    private static readonly IReadOnlyDictionary<string, LedgerAccount> ByCode =
        new[]
        {
            Bank, Cash, LoanPortfolio, InterestReceivable, LateInterestReceivable,
            LoanLossAllowance, UnappliedPayments, InterestIncome, LateInterestIncome,
            FeeIncome, LoanLossExpense, WriteOffExpense
        }.ToDictionary(a => a.Code);

    public static IReadOnlyCollection<LedgerAccount> All => ByCode.Values.ToArray();

    public static LedgerAccount FromCode(string code)
        => ByCode.TryGetValue(code, out var acc)
            ? acc
            : throw new DomainException($"Cuenta contable inexistente: '{code}'.");
}