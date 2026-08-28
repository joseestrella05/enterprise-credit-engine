// tests/Ecre.Domain.Tests/Ledger/JournalTransactionTests.cs
using Ecre.Domain.Common;
using Ecre.Domain.Ledger;
using AwesomeAssertions;

namespace Ecre.Domain.Tests.Ledger;

public class JournalTransactionTests
{
    private static readonly DateOnly Hoy = new(2026, 1, 15);
    private static Money DOP(decimal v) => Money.Of(v, Currency.DOP);

    private static JournalTransaction Balanceada() => JournalTransaction.Create(
        Hoy, "Prueba", "Test", Guid.NewGuid(),
        JournalEntry.Debit(ChartOfAccounts.LoanPortfolio, DOP(100_000m)),
        JournalEntry.Credit(ChartOfAccounts.Bank, DOP(100_000m)));

    [Fact]
    public void TransaccionDesbalanceada_NoLlegaAConstruirse()
    {
        var accion = () => JournalTransaction.Create(
            Hoy, "Descuadrada", "Test", Guid.NewGuid(),
            JournalEntry.Debit(ChartOfAccounts.LoanPortfolio, DOP(100_000m)),
            JournalEntry.Credit(ChartOfAccounts.Bank, DOP(99_999.99m)));

        accion.Should().Throw<DomainException>().WithMessage("*desbalanceada*desfase*");
    }

    [Fact]
    public void DesfaseDeUnCentavo_TambienSeRechaza()
    {
        var accion = () => JournalTransaction.Create(
            Hoy, "Un centavo", "Test", Guid.NewGuid(),
            JournalEntry.Debit(ChartOfAccounts.Bank, DOP(0.02m)),
            JournalEntry.Credit(ChartOfAccounts.InterestIncome, DOP(0.01m)));

        accion.Should().Throw<DomainException>();
    }

    [Fact]
    public void AsientoConMontoCeroONegativo_EsRechazado()
    {
        var cero = () => JournalEntry.Debit(ChartOfAccounts.Bank, DOP(0m));
        cero.Should().Throw<DomainException>().WithMessage("*estrictamente positivo*");
    }

    [Fact]
    public void MonedasMezcladas_SonRechazadas()
    {
        var accion = () => JournalTransaction.Create(
            Hoy, "Mixta", "Test", Guid.NewGuid(),
            JournalEntry.Debit(ChartOfAccounts.Bank, Money.Of(100m, Currency.DOP)),
            JournalEntry.Credit(ChartOfAccounts.InterestIncome, Money.Of(100m, Currency.USD)));

            accion.Should().Throw<DomainException>().WithMessage("*compartir moneda*");
    }

    [Fact]
    public void UnSoloAsiento_NoEsPartidaDoble()
    {
        var accion = () => JournalTransaction.Create(
            Hoy, "Coja", "Test", Guid.NewGuid(),
            JournalEntry.Debit(ChartOfAccounts.Bank, DOP(100m)));

        accion.Should().Throw<DomainException>().WithMessage("*al menos dos asientos*");
    }

    [Fact]
    public void Reversion_EspejaDireccionesYMantieneBalance()
    {
        var original = Balanceada();
        var contra = original.Reverse(new DateOnly(2026, 1, 20), "Error de digitación");

        contra.ReversesTransactionId.Should().Be(original.Id);
        contra.TotalDebits.Should().Be(original.TotalDebits);

        var cartera = contra.Entries.Single(e => e.AccountCode == ChartOfAccounts.LoanPortfolio.Code);
        cartera.Direction.Should().Be(EntryDirection.Credit);  // era débito

        // El original queda intacto (RF-05)
        original.Entries.Single(e => e.AccountCode == ChartOfAccounts.LoanPortfolio.Code)
                .Direction.Should().Be(EntryDirection.Debit);
    }

    [Fact]
    public void NoSePuedeRevertirUnaReversion()
    {
        var contra = Balanceada().Reverse(Hoy, "Motivo");
        var accion = () => contra.Reverse(Hoy, "Otro motivo");

        accion.Should().Throw<DomainException>().WithMessage("*revertir una reversión*");
    }

    [Fact]
    public void ReversionSinMotivo_EsRechazada()
    {
        var accion = () => Balanceada().Reverse(Hoy, "  ");
        accion.Should().Throw<DomainException>().WithMessage("*justificación*");
    }
}