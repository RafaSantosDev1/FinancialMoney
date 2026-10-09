namespace FinSight.Domain;

public class Budget
{
    public int Id { get; set; }

    /// <summary>Null means the budget is global (not tied to a category).</summary>
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public int Year { get; set; }
    public int Month { get; set; } // 1-12

    public decimal LimitAmount { get; set; }

    public bool IsDemo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
