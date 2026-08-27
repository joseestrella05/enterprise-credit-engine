using Ecre.Domain.Common;
using Ecre.Domain.Credits;
using Ecre.Domain.Ledger;
using FluentAssertions;

namespace Ecre.Domain.Tests.Ledger;

public class LoanPostingRulesTests
{
    private static Loan CreditoDesembolsado()
    {
        var loan = Loan.CreateDraft("CR-0001", Guid.NewGuid(), Money.Of(100_000m, Currency.DOP),
            InterestRate.FromAnnualPercentage(24m), Term.Of(12), PaymentFrequency.Monthly);
        loan.SubmitForReview();
        loan.Approve();
        loan.Disburse(new DateOnly(2025, 12, 31), new DateOnly(2026, 1, 31));
        return loan;
    }

    [Fact]
    public void Desembolso_DebitaCarteraYAcreditaBancos()
    {
        var tx = LoanPostingRules.Disbursement(CreditoDesembolsado(), new DateOnly(2025, 12, 31));

        tx.Entries.Should().HaveCount(2);
        tx.TotalDebits.Should().Be(tx.TotalCredits);

        tx.Entries.Single(e => e.Direction == EntryDirection.Debit)
          .AccountCode.Should().Be(ChartOfAccounts.LoanPortfolio.Code);
        tx.Entries.Single(e => e.Direction == EntryDirection.Credit)
          .AccountCode.Should().Be(ChartOfAccounts.Bank.Code);
    }

    [Fact]
    public void CobroDeCuota_DesglosaCapitalEInteres()
    {
        var loan = CreditoDesembolsado();
        var cuota = loan.Installments[0];

        var tx = LoanPostingRules.InstallmentCollection(
            loan, new DateOnly(2026, 1, 31),
            cuota.ScheduledPrincipal, cuota.ScheduledInterest, Money.Zero(loan.Currency));

        tx.TotalDebits.Should().Be(Money.Of(9_455.96m, Currency.DOP));
        tx.TotalCredits.Should().Be(tx.TotalDebits);
        tx.Entries.Should().HaveCount(3); // sin mora, el asiento moratorio se omite
    }

    [Fact]
    public void CobroSinMora_NoGeneraAsientoEnCero()
    {
        var loan = CreditoDesembolsado();

        var tx = LoanPostingRules.InstallmentCollection(
            loan, new DateOnly(2026, 1, 31),
            Money.Of(7_455.96m, Currency.DOP), Money.Of(2_000m, Currency.DOP),
            Money.Zero(Currency.DOP));

        tx.Entries.Should().NotContain(e => e.AccountCode == ChartOfAccounts.LateInterestIncome.Code);
    }
}