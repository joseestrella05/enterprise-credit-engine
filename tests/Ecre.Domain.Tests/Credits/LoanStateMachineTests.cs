using Ecre.Domain.Common;
using Ecre.Domain.Credits;
using FluentAssertions;

namespace Ecre.Domain.Tests.Credits;

public class LoanStateMachineTests
{
    private static Loan NewDraft() => Loan.CreateDraft(
        "CR-0001", Guid.NewGuid(), Money.Of(100_000m, Currency.DOP),
        InterestRate.FromAnnualPercentage(24m), Term.Of(12), PaymentFrequency.Monthly);

    [Fact]
    public void NoSePuedeSaltarEstados()
    {
        var loan = NewDraft();
        var accion = () => loan.Approve();   // Draft → Approved, saltando UnderReview

        accion.Should().Throw<DomainException>().WithMessage("*Draft → Approved*");
    }

    [Fact]
    public void MontosCongelados_TrasAprobacion()
    {
        var loan = NewDraft();
        loan.SubmitForReview();
        loan.Approve();

        var accion = () => loan.Amend(principal: Money.Of(200_000m, Currency.DOP));

        accion.Should().Throw<DomainException>().WithMessage("*congelados*");
    }

    [Fact]
    public void FlujoCompleto_GeneraTablaAlDesembolsar()
    {
        var loan = NewDraft();
        loan.SubmitForReview();
        loan.Approve();
        loan.Disburse(new DateOnly(2025, 12, 31), new DateOnly(2026, 1, 31));
        loan.Activate();

        loan.Status.Should().Be(LoanStatus.Active);
        loan.Installments.Should().HaveCount(12);
        loan.Installments.Sum(x => x.ScheduledPrincipal.Amount).Should().Be(100_000m);
    }

    [Fact]
    public void EstadosTerminales_NoAdmitenSalida()
    {
        LoanStateMachine.IsTerminal(LoanStatus.FullyPaid).Should().BeTrue();
        LoanStateMachine.IsTerminal(LoanStatus.Defaulted).Should().BeTrue();
    }
}