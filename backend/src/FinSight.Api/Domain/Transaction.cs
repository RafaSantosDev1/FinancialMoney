namespace FinSight.Domain;

public class Transaction
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>Always stored as a positive magnitude. <see cref="Type"/> distinguishes income from expense.</summary>
    public decimal Amount { get; set; }

    public TransactionType Type { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public int? AccountId { get; set; }
    public Account? Account { get; set; }

    public string? Notes { get; set; }
    public string? ExternalReference { get; set; }

    public int? ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
