using Ecre.Domain.Common;
using Ecre.Domain.Credits;

namespace Ecre.Domain.Ledger;

public static class LoanPostingRules
{
    public const string SourceLoan = "Loan";

    /// Desembolso: DR Cartera (Activo↑) / CR Bancos (Activo↓).
    /// Cambia la composición del activo, no su total.
    public static JournalTransaction Disbursement(Loan loan, DateOnly bookingDate)
    {
        if (loan.Status != LoanStatus.Disbursed && loan.Status != LoanStatus.Active)
            throw new DomainException(
                $"Solo se contabiliza el desembolso de un crédito desembolsado. Estado actual: {loan.Status}.");

        return JournalTransaction.Create(
            bookingDate,
            $"Desembolso crédito {loan.Number}",
            SourceLoan, loan.Id,
            JournalEntry.Debit(ChartOfAccounts.LoanPortfolio, loan.Principal, $"Capital {loan.Number}"),
            JournalEntry.Credit(ChartOfAccounts.Bank, loan.Principal, "Salida de efectivo"));
    }

    /// Cobro de cuota: DR Bancos / CR Cartera + CR Ingresos por Intereses.
    /// Los componentes en cero se omiten: un asiento de $0 no aporta información.
    public static JournalTransaction InstallmentCollection(
        Loan loan, DateOnly bookingDate,
        Money principalPortion, Money interestPortion, Money lateInterestPortion)
    {
        var currency = loan.Currency;
        var total = principalPortion + interestPortion + lateInterestPortion;

        if (!total.IsPositive)
            throw new DomainException($"El cobro debe ser positivo. Recibido: {total}.");

        var entries = new List<JournalEntry>
        {
            JournalEntry.Debit(ChartOfAccounts.Bank, total, $"Cobro crédito {loan.Number}")
        };

        if (principalPortion.IsPositive)
            entries.Add(JournalEntry.Credit(ChartOfAccounts.LoanPortfolio, principalPortion, "Abono a capital"));

        if (interestPortion.IsPositive)
            entries.Add(JournalEntry.Credit(ChartOfAccounts.InterestIncome, interestPortion, "Interés corriente"));

        if (lateInterestPortion.IsPositive)
            entries.Add(JournalEntry.Credit(ChartOfAccounts.LateInterestIncome, lateInterestPortion, "Interés moratorio"));

        return JournalTransaction.Create(
            bookingDate, $"Cobro cuota crédito {loan.Number}", SourceLoan, loan.Id,
            entries.ToArray());
    }

    /// Castigo: DR Gasto por castigo / CR Cartera. Saca el capital irrecuperable.
    public static JournalTransaction WriteOff(Loan loan, DateOnly bookingDate, Money outstandingPrincipal)
    {
        if (!outstandingPrincipal.IsPositive)
            throw new DomainException("El castigo requiere capital pendiente positivo.");

        return JournalTransaction.Create(
            bookingDate, $"Castigo de cartera crédito {loan.Number}", SourceLoan, loan.Id,
            JournalEntry.Debit(ChartOfAccounts.WriteOffExpense, outstandingPrincipal, "Pérdida por incobrabilidad"),
            JournalEntry.Credit(ChartOfAccounts.LoanPortfolio, outstandingPrincipal, $"Baja de {loan.Number}"));
    }
}