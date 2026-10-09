using FinSight.Domain;
using FinSight.Dtos;

namespace FinSight.Services;

/// <summary>
/// Pure financial calculations. All money values are stored as positive magnitudes
/// and classified through <see cref="TransactionType"/>.
/// </summary>
public static class FinanceCalculator
{
    public static decimal TotalIncome(IEnumerable<Transaction> transactions) =>
        transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);

    public static decimal TotalExpenses(IEnumerable<Transaction> transactions) =>
        transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);

    public static decimal NetBalance(decimal totalIncome, decimal totalExpenses) =>
        totalIncome - totalExpenses;

    public static decimal? AverageExpensePerTransaction(decimal totalExpenses, int expenseCount) =>
        expenseCount > 0 && totalExpenses > 0m ? decimal.Round(totalExpenses / expenseCount, 2) : null;

    /// <summary>Total expenses divided by the number of calendar days in the analysed period.</summary>
    public static decimal? AverageDailyExpense(decimal totalExpenses, int daysInPeriod) =>
        daysInPeriod > 0 && totalExpenses > 0m ? decimal.Round(totalExpenses / daysInPeriod, 2) : null;

    /// <summary>Total expenses divided by the number of distinct calendar months touched by the period.</summary>
    public static decimal? AverageMonthlyExpense(decimal totalExpenses, int monthsInPeriod) =>
        monthsInPeriod > 0 && totalExpenses > 0m ? decimal.Round(totalExpenses / monthsInPeriod, 2) : null;

    /// <summary>Net balance / income * 100. Null when there is no income.</summary>
    public static decimal? SavingsRate(decimal netBalance, decimal totalIncome) =>
        totalIncome > 0m ? decimal.Round(netBalance / totalIncome * 100m, 2) : null;

    /// <summary>Percentage change relative to the previous value. Null when the previous value is zero.</summary>
    public static decimal? PercentChange(decimal current, decimal previous) =>
        previous != 0m
            ? decimal.Round((current - previous) / Math.Abs(previous) * 100m, 2)
            : null;

    public static MetricDeltaDto Delta(decimal current, decimal previous, bool higherIsBetter) =>
        new(current, previous, current - previous, PercentChange(current, previous), higherIsBetter);

    public static int DaysInRange(DateOnly from, DateOnly to) =>
        Math.Max(0, to.ToDateTime(TimeOnly.MinValue).Subtract(from.ToDateTime(TimeOnly.MinValue)).Days + 1);

    public static int MonthsInRange(DateOnly from, DateOnly to)
    {
        if (to < from) return 0;
        return (to.Year - from.Year) * 12 + (to.Month - from.Month) + 1;
    }

    /// <summary>Previous period of identical length, ending the day before <paramref name="from"/>.</summary>
    public static (DateOnly From, DateOnly To) PreviousPeriod(DateOnly from, DateOnly to)
    {
        var days = DaysInRange(from, to);
        var previousTo = from.AddDays(-1);
        var previousFrom = previousTo.AddDays(-(days - 1));
        return (previousFrom, previousTo);
    }
}
