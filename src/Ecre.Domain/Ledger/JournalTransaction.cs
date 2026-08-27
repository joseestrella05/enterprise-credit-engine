using Ecre.Domain.Common;

namespace Ecre.Domain.Ledger;

public sealed class JournalTransaction
{
    private readonly List<JournalEntry> _entries = new();

    public Guid Id { get; private set; }
    public DateOnly BookingDate { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public string Description { get; private set; } = null!;
    public string SourceType { get; private set; } = null!;
    public Guid SourceId { get; private set; }
    public Currency Currency { get; private set; }
    public Guid? ReversesTransactionId { get; private set; }

    public IReadOnlyList<JournalEntry> Entries => _entries.AsReadOnly();
    public bool IsReversal => ReversesTransactionId is not null;

    private JournalTransaction() { } 

    private JournalTransaction(
        DateOnly bookingDate, string description, string sourceType, Guid sourceId,
        IEnumerable<JournalEntry> entries, Guid? reverses)
    {
        var list = entries?.ToList()
            ?? throw new DomainException("Una transacción requiere asientos.");

        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Toda transacción contable requiere descripción.");

        EnsureBalanced(list);

        Id = Guid.NewGuid();
        BookingDate = bookingDate;
        RecordedAt = DateTimeOffset.UtcNow;
        Description = description.Trim();
        SourceType = sourceType.Trim();
        SourceId = sourceId;
        Currency = list[0].Amount.Currency;
        ReversesTransactionId = reverses;
        _entries.AddRange(list);
    }

    public static JournalTransaction Create(
        DateOnly bookingDate, string description, string sourceType, Guid sourceId,
        params JournalEntry[] entries)
        => new(bookingDate, description, sourceType, sourceId, entries, reverses: null);

    public Money TotalDebits  => SumOf(EntryDirection.Debit);
    public Money TotalCredits => SumOf(EntryDirection.Credit);

    private Money SumOf(EntryDirection dir)
        => _entries.Where(e => e.Direction == dir)
                   .Aggregate(Money.Zero(Currency), (acc, e) => acc + e.Amount);

    /// RF-06. Se ejecuta en el constructor: una transacción desbalanceada
    /// no puede existir como objeto, mucho menos llegar a la base de datos.
    private static void EnsureBalanced(IReadOnlyList<JournalEntry> entries)
    {
        if (entries.Count < 2)
            throw new DomainException(
                $"La partida doble exige al menos dos asientos. Recibidos: {entries.Count}.");

        var currency = entries[0].Amount.Currency;
        if (entries.Any(e => !e.Amount.Currency.Equals(currency)))
            throw new DomainException(
                "Todos los asientos de una transacción deben compartir moneda. " +
                "Use transacciones separadas con cuenta puente de conversión.");

        var debits  = entries.Where(e => e.Direction == EntryDirection.Debit)
                             .Aggregate(Money.Zero(currency), (a, e) => a + e.Amount);
        var credits = entries.Where(e => e.Direction == EntryDirection.Credit)
                             .Aggregate(Money.Zero(currency), (a, e) => a + e.Amount);

        var delta = debits - credits;
        if (!delta.IsZero)
            throw new DomainException(
                $"Transacción desbalanceada. Débitos {debits}, Créditos {credits}, desfase {delta}.");
    }

    /// RF-05: la única forma de corregir es un contra-asiento.
    public JournalTransaction Reverse(DateOnly bookingDate, string reason)
    {
        if (IsReversal)
            throw new DomainException(
                "No se puede revertir una reversión. Registre un asiento de ajuste nuevo.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Toda reversión exige justificación.");

        if (bookingDate < BookingDate)
            throw new DomainException(
                $"La reversión ({bookingDate}) no puede anteceder al asiento original ({BookingDate}).");

        return new JournalTransaction(
            bookingDate,
            $"REVERSIÓN de '{Description}'. Motivo: {reason.Trim()}",
            SourceType, SourceId,
            _entries.Select(e => e.Mirror()),
            reverses: Id);
    }

    public override string ToString()
        => $"[{BookingDate:yyyy-MM-dd}] {Description} — {_entries.Count} asientos, {TotalDebits}";
}