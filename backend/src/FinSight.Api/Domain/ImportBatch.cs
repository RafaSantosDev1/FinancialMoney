namespace FinSight.Domain;

public class ImportBatch
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public int TotalRows { get; set; }
    public int ImportedRows { get; set; }
    public int RejectedRows { get; set; }
    public int DuplicateRows { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
