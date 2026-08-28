using Ecre.Domain.Common;
using Ecre.Domain.Credits;
using AwesomeAssertions;

namespace Ecre.Domain.Tests.Credits;

/// Crédito de referencia para toda la Fase 3:
/// 100,000 DOP · 24% anual · 12 cuotas mensuales (cuota = 9,455.96) · mora 36%.
public class LoanPaymentTests
{
    private static readonly Currency DOP = Currency.DOP;
    private static readonly DateOnly Disbursed = new(2026, 1, 15);
    private static readonly DateOnly FirstDue  = new(2026, 2, 15);

    private static Money Dop(decimal amount) => Money.Of(amount, DOP);

    private static Loan ActiveLoan(LatePolicy? policy = null)
    {
        var loan = Loan.CreateDraft(
            number: "CRE-0001",
            borrowerId: Guid.NewGuid(),
            principal: Dop(100_000m),
            rate: InterestRate.FromAnnualPercentage(24m),
            term: Term.Of(12),
            frequency: PaymentFrequency.Monthly,
            latePolicy: policy ?? LatePolicy.Default(InterestRate.FromAnnualPercentage(36m)));

        loan.SubmitForReview();
        loan.Approve();
        loan.Disburse(Disbursed, FirstDue);
        loan.Activate();

        return loan;
    }

    private static Loan DraftLoan()
        => Loan.CreateDraft(
            "CRE-0002", Guid.NewGuid(), Dop(100_000m),
            InterestRate.FromAnnualPercentage(24m), Term.Of(12), PaymentFrequency.Monthly);

    // ---------------------------------------------------------------- tabla base

    [Fact]
    public void TablaBase_ArrancaConLaCuotaEsperada()
    {
        var loan = ActiveLoan();

        loan.Installments.Should().HaveCount(12);
        loan.Installments[0].ScheduledTotal.Should().Be(Dop(9_455.96m));
        loan.Installments[0].ScheduledInterest.Should().Be(Dop(2_000m));
        loan.Installments[0].ScheduledPrincipal.Should().Be(Dop(7_455.96m));
        loan.OutstandingPrincipal.Should().Be(Dop(100_000m));
    }

    // ---------------------------------------------------------------- RF-03 prelación

    [Fact]
    public void PagoExacto_LiquidaLaCuotaCompleta()
    {
        var loan = ActiveLoan();

        var result = loan.ApplyPayment(Dop(9_455.96m), FirstDue);

        result.Principal.Should().Be(Dop(7_455.96m));
        result.Interest.Should().Be(Dop(2_000m));
        result.LateInterest.Should().Be(Dop(0m));
        result.Unapplied.Should().Be(Dop(0m));

        loan.Installments[0].Status.Should().Be(InstallmentStatus.Paid);
        loan.Installments[0].IsSettled.Should().BeTrue();
        loan.OutstandingPrincipal.Should().Be(Dop(92_544.04m));
    }

    [Fact]
    public void PagoParcial_VaTodoAInteres_YNadaACapital()
    {
        var loan = ActiveLoan();

        var result = loan.ApplyPayment(Dop(1_500m), FirstDue);

        result.Interest.Should().Be(Dop(1_500m));
        result.Principal.Should().Be(Dop(0m));
        result.LateInterest.Should().Be(Dop(0m));

        loan.Installments[0].Status.Should().Be(InstallmentStatus.PartiallyPaid);
        loan.Installments[0].OutstandingInterest.Should().Be(Dop(500m));
        loan.Installments[0].OutstandingPrincipal.Should().Be(Dop(7_455.96m));
    }

    [Fact]
    public void ConMoraAcumulada_ElPagoLiquidaMoraAntesQueInteresYCapital()
    {
        var loan = ActiveLoan();
        var paidOn = FirstDue.AddDays(10);   // 3 de gracia + 7 días de mora

        // 7,455.96 × (0.36/360) × 7 = 52.19
        var result = loan.ApplyPayment(Dop(100m), paidOn);

        result.LateInterest.Should().Be(Dop(52.19m));
        result.Interest.Should().Be(Dop(47.81m));
        result.Principal.Should().Be(Dop(0m));

        loan.Installments[0].PaidLateInterest.Should().Be(Dop(52.19m));
        loan.Installments[0].OutstandingLateInterest.Should().Be(Dop(0m));
    }

    [Fact]
    public void PagoGrande_CascadeaALaCuotaSiguiente()
    {
        var loan = ActiveLoan();

        var result = loan.ApplyPayment(Dop(15_000m), FirstDue);

        result.Allocations.Should().HaveCount(2);
        result.Allocations[0].InstallmentNumber.Should().Be(1);
        result.Allocations[1].InstallmentNumber.Should().Be(2);

        loan.Installments[0].Status.Should().Be(InstallmentStatus.Paid);

        // Remanente 5,544.04 → interés cuota 2 (1,850.88) y el resto a capital.
        loan.Installments[1].PaidInterest.Should().Be(Dop(1_850.88m));
        loan.Installments[1].PaidPrincipal.Should().Be(Dop(3_693.16m));
        loan.Installments[1].Status.Should().Be(InstallmentStatus.PartiallyPaid);

        result.Unapplied.Should().Be(Dop(0m));
    }

    [Fact]
    public void PagarElTotal_DejaElCreditoEnFullyPaid()
    {
        var loan = ActiveLoan();
        var total = loan.TotalOutstanding;

        var result = loan.ApplyPayment(total, FirstDue);

        result.LoanFullyPaid.Should().BeTrue();
        result.Unapplied.Should().Be(Dop(0m));
        loan.Status.Should().Be(LoanStatus.FullyPaid);
        loan.OutstandingPrincipal.Should().Be(Dop(0m));
        loan.Installments.Should().OnlyContain(x => x.Status == InstallmentStatus.Paid);
    }

    [Fact]
    public void ExcedenteSobreElTotal_ApareceComoUnapplied_NoComoIngreso()
    {
        var loan = ActiveLoan();
        var total = loan.TotalOutstanding;

        var result = loan.ApplyPayment(total + Dop(1_000m), FirstDue);

        result.Unapplied.Should().Be(Dop(1_000m));
        result.Applied.Should().Be(total);
        result.Principal.Should().Be(Dop(100_000m));   // ni un peso extra a capital
        loan.Status.Should().Be(LoanStatus.FullyPaid);
    }

    // ---------------------------------------------------------------- RF-04 mora

    [Fact]
    public void LaMoraSeCalculaSoloSobreElCapitalDeLaCuotaVencida_NoSobreElSaldoTotal()
    {
        var loan = ActiveLoan();
        var asOf = FirstDue.AddDays(10);

        loan.AccrueLateInterest(asOf);

        // Base = capital de la cuota 1 (7,455.96), no el saldo de 100,000.
        loan.OverduePrincipal(asOf).Should().Be(Dop(7_455.96m));
        loan.Installments[0].AccruedLateInterest.Should().Be(Dop(52.19m));

        // La cuota 2 aún no vence: mora cero.
        loan.Installments[1].AccruedLateInterest.Should().Be(Dop(0m));
        loan.Installments[1].Status.Should().Be(InstallmentStatus.Pending);

        loan.OutstandingLateInterest.Should().Be(Dop(52.19m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void DentroDeLosTresDiasDeGracia_NoSeDevengaNada(int days)
    {
        var loan = ActiveLoan();
        var asOf = FirstDue.AddDays(days);

        var accrued = loan.AccrueLateInterest(asOf);

        accrued.Should().Be(Dop(0m));
        loan.Installments[0].AccruedLateInterest.Should().Be(Dop(0m));
        loan.Installments[0].Status.Should().Be(InstallmentStatus.Pending);
        loan.OverduePrincipal(asOf).Should().Be(Dop(0m));
    }

    [Fact]
    public void LaMoraCorreDesdeQueExpiraLaGracia_NoDesdeElVencimiento()
    {
        var loan = ActiveLoan();

        // Día 4: un solo día de mora, no cuatro.
        loan.AccrueLateInterest(FirstDue.AddDays(4));

        loan.Installments[0].AccruedLateInterest.Should().Be(Dop(7.46m)); // 7,455.96 × 0.001
        loan.Installments[0].Status.Should().Be(InstallmentStatus.Overdue);
    }

    [Fact]
    public void DevengarDosVecesLaMismaFecha_NoDuplica()
    {
        var loan = ActiveLoan();
        var asOf = FirstDue.AddDays(10);

        var first  = loan.AccrueLateInterest(asOf);
        var second = loan.AccrueLateInterest(asOf);

        first.Should().Be(Dop(52.19m));
        second.Should().Be(Dop(0m));
        loan.Installments[0].AccruedLateInterest.Should().Be(Dop(52.19m));
        loan.Installments[0].LateAccruedThrough.Should().Be(asOf);
    }

    [Fact]
    public void ElDevengoEsIncremental_ContinuaDesdeElUltimoCorte()
    {
        var loan = ActiveLoan();

        loan.AccrueLateInterest(FirstDue.AddDays(10));   // 7 días
        loan.AccrueLateInterest(FirstDue.AddDays(13));   // +3 días

        // 7,455.96 × 0.001 × 10 = 74.56 (52.19 + 22.37)
        loan.Installments[0].AccruedLateInterest.Should().Be(Dop(74.56m));
    }

    [Fact]
    public void NoHayAnatocismo_LaMoraNoDevengaMora()
    {
        var loan = ActiveLoan();

        loan.AccrueLateInterest(FirstDue.AddDays(13));   // 10 días de una sola vez
        var deUnaVez = loan.Installments[0].AccruedLateInterest;

        var otro = ActiveLoan();
        otro.AccrueLateInterest(FirstDue.AddDays(8));
        otro.AccrueLateInterest(FirstDue.AddDays(13));
        var enDosTramos = otro.Installments[0].AccruedLateInterest;

        enDosTramos.Should().Be(deUnaVez);   // la mora acumulada nunca entra en la base
    }

    [Fact]
    public void BaseActual365_DevengaMenosQueActual360()
    {
        var a360 = ActiveLoan();
        var a365 = ActiveLoan(new LatePolicy(3, InterestRate.FromAnnualPercentage(36m), DayCountBasis.Actual365));
        var asOf = FirstDue.AddDays(10);

        a360.AccrueLateInterest(asOf);
        a365.AccrueLateInterest(asOf);

        a365.OutstandingLateInterest.Should().BeLessThan(a360.OutstandingLateInterest);
    }

    // ---------------------------------------------------------------- RF-03 prepagos

    [Fact]
    public void PrepagoConReduceTerm_AcortaLaTabla_YMantieneLaCuota()
    {
        var loan = ActiveLoan();
        loan.ApplyPayment(Dop(9_455.96m), FirstDue);          // cuota 1 saldada

        var prepayOn = FirstDue.AddDays(1);
        var result = loan.ApplyPrepayment(Dop(20_000m), prepayOn, PrepaymentMode.ReduceTerm);

        result.PrincipalPrepaid.Should().Be(Dop(20_000m));
        result.Unapplied.Should().Be(Dop(0m));
        result.NewRemainingInstallments.Should().BeLessThan(result.PreviousRemainingInstallments);
        result.InstallmentsSaved.Should().BePositive();

        // Misma cuota: las cuotas regulares no se mueven.
        result.NewInstallmentAmount.Should().Be(result.PreviousInstallmentAmount);

        // La cuota 1 pagada se preserva intacta.
        loan.Installments[0].Status.Should().Be(InstallmentStatus.Paid);
        loan.Installments[0].ScheduledTotal.Should().Be(Dop(9_455.96m));

        loan.OutstandingPrincipal.Should().Be(Dop(72_544.04m));
    }

    [Fact]
    public void PrepagoConReduceInstallment_MantieneElPlazo_YBajaLaCuota()
    {
        var loan = ActiveLoan();
        loan.ApplyPayment(Dop(9_455.96m), FirstDue);

        var prepayOn = FirstDue.AddDays(1);
        var result = loan.ApplyPrepayment(Dop(20_000m), prepayOn, PrepaymentMode.ReduceInstallment);

        result.NewRemainingInstallments.Should().Be(result.PreviousRemainingInstallments);
        result.InstallmentsSaved.Should().Be(0);
        result.NewInstallmentAmount.Should().BeLessThan(result.PreviousInstallmentAmount);

        loan.Installments.Should().HaveCount(12);
        loan.OutstandingPrincipal.Should().Be(Dop(72_544.04m));
    }

    [Fact]
    public void ElPrepago_SaldaPrimeroLoVencido_YSoloElRemanenteAbonaACapital()
    {
        var loan = ActiveLoan();
        var prepayOn = FirstDue.AddDays(10);   // cuota 1 vencida con 7 días de mora

        var result = loan.ApplyPrepayment(Dop(30_000m), prepayOn, PrepaymentMode.ReduceTerm);

        // Vencido = 7,455.96 capital + 2,000 interés + 52.19 mora = 9,508.15
        result.Arrears.LateInterest.Should().Be(Dop(52.19m));
        result.Arrears.Interest.Should().Be(Dop(2_000m));
        result.Arrears.Principal.Should().Be(Dop(7_455.96m));
        result.PrincipalPrepaid.Should().Be(Dop(30_000m) - Dop(9_508.15m));

        loan.Installments[0].Status.Should().Be(InstallmentStatus.Paid);
    }

    [Fact]
    public void ElRecalculoPreservaLasCuotasYaPagadas()
    {
        var loan = ActiveLoan();
        loan.ApplyPayment(Dop(9_455.96m), FirstDue);
        var pagada = loan.Installments[0];
        var interesOriginal = pagada.ScheduledInterest;

        loan.ApplyPrepayment(Dop(20_000m), FirstDue.AddDays(1), PrepaymentMode.ReduceInstallment);

        pagada.ScheduledInterest.Should().Be(interesOriginal);
        pagada.PaidPrincipal.Should().Be(Dop(7_455.96m));
        pagada.Status.Should().Be(InstallmentStatus.Paid);
    }

    [Fact]
    public void PrepagoQueSuperaElCapitalVivo_DejaExcedenteSinAplicar_YCierraElCredito()
    {
        var loan = ActiveLoan();

        var result = loan.ApplyPrepayment(Dop(200_000m), FirstDue, PrepaymentMode.ReduceTerm);

        result.Unapplied.IsPositive.Should().BeTrue();
        result.LoanFullyPaid.Should().BeTrue();
        loan.Status.Should().Be(LoanStatus.FullyPaid);
        loan.OutstandingPrincipal.Should().Be(Dop(0m));
    }

    // ---------------------------------------------------------------- guardas

    [Fact]
    public void AplicarUnPagoAUnCreditoNoActive_LanzaDomainException()
    {
        var loan = DraftLoan();

        var act = () => loan.ApplyPayment(Dop(1_000m), FirstDue);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AplicarUnPrepagoAUnCreditoNoActive_LanzaDomainException()
    {
        var loan = DraftLoan();

        var act = () => loan.ApplyPrepayment(Dop(1_000m), FirstDue, PrepaymentMode.ReduceTerm);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void PagarUnCreditoYaSaldado_LanzaDomainException()
    {
        var loan = ActiveLoan();
        loan.ApplyPayment(loan.TotalOutstanding, FirstDue);

        var act = () => loan.ApplyPayment(Dop(100m), FirstDue.AddDays(1));

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public void PagoNoPositivo_LanzaDomainException(decimal amount)
    {
        var loan = ActiveLoan();

        var act = () => loan.ApplyPayment(Dop(amount), FirstDue);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void PagoEnMonedaDistinta_LanzaDomainException()
    {
        var loan = ActiveLoan();

        var act = () => loan.ApplyPayment(Money.Of(100m, Currency.USD), FirstDue);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void LatePolicyPorDefecto_UsaLaMismaTasaQueLaCorriente()
    {
        var loan = DraftLoan();

        loan.LatePolicy.AnnualLateRate.Should().Be(InterestRate.FromAnnualPercentage(24m));
        loan.LatePolicy.GraceDays.Should().Be(3);
        loan.LatePolicy.Basis.Should().Be(DayCountBasis.Actual360);
    }

    [Fact]
    public void ElAgregadoNoExponeSuListaMutable()
    {
        var loan = ActiveLoan();

        loan.Installments.Should().NotBeAssignableTo<List<Installment>>();
    }
}
