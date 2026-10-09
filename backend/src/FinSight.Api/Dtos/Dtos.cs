using FinSight.Domain;

namespace FinSight.Dtos;

// ---------- Shared ----------

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public record CategoryDto(int Id, string Name, string Color, CategoryAppliesTo AppliesTo, bool IsArchived, bool IsDemo);

public record CategoryUpsertRequest(string Name, string? Color, CategoryAppliesTo AppliesTo, bool IsArchived);

public record AccountDto(int Id, string Name, AccountType AccountType, bool IsArchived, bool IsDemo);

public record AccountUpsertRequest(string Name, AccountType AccountType, bool IsArchived);

// ---------- Transactions ----------

public record TransactionDto(
    int Id,
    DateOnly Date,
    string Description,
    decimal Amount,
    TransactionType Type,
    int CategoryId,
    string CategoryName,
    string CategoryColor,
    int? AccountId,
    string? AccountName,
    string? Notes,
    string? ExternalReference,
    int? ImportBatchId,
    DateTime CreatedAt);

public class TransactionQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public List<int>? CategoryIds { get; set; }
    public TransactionType? Type { get; set; }
    public int? AccountId { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "date";
    public bool Descending { get; set; } = true;
}

public record TransactionCreateRequest(
    DateOnly Date,
    string Description,
    decimal Amount,
    TransactionType Type,
    int CategoryId,
    int? AccountId,
    string? Notes);

public record TransactionUpdateRequest(
    DateOnly Date,
    string Description,
    decimal Amount,
    TransactionType Type,
    int CategoryId,
    int? AccountId,
    string? Notes);

public record ImportReportDto(
    string FileName,
    DateTime ImportedAt,
    int TotalRows,
    int ImportedRows,
    int RejectedRows,
    int DuplicateRows);

// ---------- Budgets ----------

public record BudgetDto(
    int Id,
    int? CategoryId,
    string? CategoryName,
    int Year,
    int Month,
    decimal LimitAmount,
    bool IsDemo);

public record BudgetUpsertRequest(int? CategoryId, int Year, int Month, decimal LimitAmount);

public record BudgetStatusDto(
    int? CategoryId,
    string? CategoryName,
    string? CategoryColor,
    int Year,
    int Month,
    decimal LimitAmount,
    decimal SpentAmount,
    decimal RemainingAmount,
    decimal SpentPercent,
    BudgetHealth Health,
    bool IsDemo);

public enum BudgetHealth
{
    NoLimit = 0,
    Ok = 1,
    Warning = 2,
    Exceeded = 3
}

// ---------- Dashboard ----------

public record DashboardSummaryDto(
    DateOnly From,
    DateOnly To,
    int DaysInPeriod,
    int MonthsInPeriod,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal NetBalance,
    int TransactionCount,
    int IncomeCount,
    int ExpenseCount,
    decimal? AverageExpensePerTransaction,
    decimal? AverageDailyExpense,
    decimal? AverageMonthlyExpense,
    decimal? SavingsRate,
    PeriodComparisonDto PreviousPeriod,
    BudgetUsageDto BudgetUsage);

public record PeriodComparisonDto(
    DateOnly From,
    DateOnly To,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal NetBalance,
    MetricDeltaDto IncomeDelta,
    MetricDeltaDto ExpenseDelta,
    NetBalanceDeltaDto BalanceDelta,
    bool HasComparison);

/// <summary>Describes how a change should be interpreted (e.g. lower expenses is good, lower income is bad).</summary>
public record MetricDeltaDto(decimal Current, decimal Previous, decimal AbsoluteChange, decimal? PercentChange, bool HigherIsBetter);

public record NetBalanceDeltaDto(decimal Current, decimal Previous, decimal AbsoluteChange, decimal? PercentChange);

public record BudgetUsageDto(
    bool HasBudgets,
    decimal TotalLimit,
    decimal TotalSpent,
    decimal RemainingAmount,
    decimal UsedPercent);

public record MonthlyTrendPointDto(
    int Year,
    int Month,
    string Label,
    decimal Income,
    decimal Expenses,
    decimal Balance,
    int TransactionCount);

public record CategoryBreakdownItemDto(
    int CategoryId,
    string CategoryName,
    string Color,
    decimal Amount,
    decimal PercentOfTotal,
    int TransactionCount);

public record WeekdaySpendingItemDto(
    int Weekday,
    string Label,
    decimal TotalAmount,
    decimal AveragePerOccurrence,
    int TransactionCount);

public record TopExpenseItemDto(
    int Id,
    DateOnly Date,
    string Description,
    decimal Amount,
    string CategoryName,
    string Color,
    string? AccountName);

public record InsightDto(
    string Kind,
    string Title,
    string Description,
    InsightSeverity Severity,
    bool IsPattern,
    decimal? Value);

public enum InsightSeverity
{
    Info = 0,
    Positive = 1,
    Warning = 2,
    Danger = 3
}
