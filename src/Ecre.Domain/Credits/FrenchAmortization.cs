using Ecre.Domain.Common;

namespace Ecre.Domain.Credits;

public static class FrenchAmortization
{
    public static IReadOnlyList<ScheduledInstallment> Build(
        Money principal,
        InterestRate rate,
        Term term,
        PaymentFrequency frequency,
        DateOnly firstDueDate)
    {
        if (!principal.IsPositive)
            throw new DomainException($"El capital debe ser positivo. Recibido: {principal}.");

        var currency = principal.Currency;
        var n = term.Installments;
        var i = rate.PeriodicNominal(frequency);

        var fixedPayment = FixedPayment(principal, i, n);

        var balance = principal;
        var schedule = new List<ScheduledInstallment>(n);

        for (int k = 1; k <= n; k++)
        {
            var interest = i == 0m
                ? Money.Zero(currency)
                : Money.Of(balance.Amount * i, currency);

            Money principalPart;
            Money total;

            if (k == n)
            {
                principalPart = balance;
                total = principalPart + interest;
            }
            else
            {
                principalPart = fixedPayment - interest;

                if (!principalPart.IsPositive)
                    throw new DomainException(
                        $"Cuota negativamente amortizable en el período {k}: la cuota {fixedPayment} " +
                        $"no cubre el interés {interest}. Revise tasa y plazo.");

                total = fixedPayment;
            }

            balance -= principalPart;

            schedule.Add(new ScheduledInstallment(
                Number: k,
                DueDate: DueDateGenerator.For(firstDueDate, frequency, k - 1),
                Principal: principalPart,
                Interest: interest,
                Total: total,
                EndingBalance: balance));
        }

        EnsureInvariants(schedule, principal);
        return schedule;
    }
    private static Money FixedPayment(Money principal, decimal i, int n)
    {
        if (i == 0m)                                    // Trampa 1
            return Money.Of(principal.Amount / n, principal.Currency);

        var factor = DecimalMath.Pow(1m + i, n);        // sin pasar por double
        var raw = principal.Amount * (i * factor) / (factor - 1m);

        return Money.Of(raw, principal.Currency);
    }

    private static void EnsureInvariants(IReadOnlyList<ScheduledInstallment> schedule, Money principal)
    {
        var currency = principal.Currency;

        var sumPrincipal = schedule.Aggregate(Money.Zero(currency), (a, x) => a + x.Principal);
        if (sumPrincipal != principal)
            throw new DomainException(
                $"Invariante roto: capital amortizado {sumPrincipal} ≠ capital otorgado {principal}.");

        if (!schedule[^1].EndingBalance.IsZero)
            throw new DomainException(
                $"Invariante roto: saldo final {schedule[^1].EndingBalance} debe ser cero.");
    }
}