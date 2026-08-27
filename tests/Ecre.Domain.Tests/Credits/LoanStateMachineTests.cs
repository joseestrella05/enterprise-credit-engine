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

    [Fact]
    public void TodoEstadoDelEnum_EstaDeclaradoEnLaMaquina()
    {
        // Guarda contra el olvido: agregar al enum sin agregar al mapa rompe la suite.
        foreach (LoanStatus status in Enum.GetValues<LoanStatus>())
        {
            var accion = () => LoanStateMachine.AllowedTargets(status);
            accion.Should().NotThrow($"'{status}' debe tener transiciones declaradas");
        }
    }

    [Fact]
    public void Rechazo_SoloDesdeUnderReview()
    {
        var loan = NewDraft();
        var desdeDraft = () => loan.Reject("Sin capacidad de pago");
        desdeDraft.Should().Throw<DomainException>().WithMessage("*Draft → Rejected*");

        loan.SubmitForReview();
        loan.Reject("Sin capacidad de pago");

        loan.Status.Should().Be(LoanStatus.Rejected);
        loan.ClosingReason.Should().Be("Sin capacidad de pago");
        LoanStateMachine.IsTerminal(LoanStatus.Rejected).Should().BeTrue();
    }

    [Fact]
    public void Cancelacion_PermitidaDesdeDraftYApproved()
    {
        var borrador = NewDraft();
        borrador.Cancel("Cliente desistió");
        borrador.Status.Should().Be(LoanStatus.Cancelled);

        var aprobado = NewDraft();
        aprobado.SubmitForReview();
        aprobado.Approve();
        aprobado.Cancel("Cliente consiguió mejor tasa");
        aprobado.Status.Should().Be(LoanStatus.Cancelled);
    }

    [Fact]
    public void Cancelacion_ImposibleTrasDesembolso()
    {
        var loan = NewDraft();
        loan.SubmitForReview();
        loan.Approve();
        loan.Disburse(new DateOnly(2025, 12, 31), new DateOnly(2026, 1, 31));

        var accion = () => loan.Cancel("Quiero devolver el dinero");

        accion.Should().Throw<DomainException>().WithMessage("*Disbursed → Cancelled*");
    }

    [Fact]
    public void CierreSinJustificacion_EsRechazado()
    {
        var loan = NewDraft();
        var accion = () => loan.Cancel("   ");
        accion.Should().Throw<DomainException>().WithMessage("*justificación*");
    }
}