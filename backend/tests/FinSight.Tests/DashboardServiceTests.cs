using FinSight.Domain;
using FinSight.Services;
using Xunit;

namespace FinSight.Tests;

public class DashboardServiceTests
{
    private static DashboardQuery Query(DateOnly from, DateOnly to, TransactionType? type = null, List<int>? categoryIds = null) =>
        new() { From = from, To = to, Type = type, CategoryIds = categoryIds };

    [Fact]
    public async Task Summary_Computes_Totals_Averages_And_Savings_Rate()
    {
        await using var db = await DbHelper.CreateSeededContextAsync();
        var service = new DashboardService(db);

        var summary = await service.SummarizeAsync(Query(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));

        Assert.Equal(3000m, summary.TotalIncome);
        Assert.Equal(1250m, summary.TotalExpenses);
        Assert.Equal(1750m, summary.NetBalance);
        Assert.Equal(4, summary.TransactionCount);
        Assert.Equal(3, summary.ExpenseCount);

        Assert.Equal(416.67m, summary.AverageExpensePerTransaction);
        Assert.Equal(40.32m, summary.AverageDailyExpense);
        Assert.Equal(1250m, summary.AverageMonthlyExpense);
        Assert.Equal(58.33m, summary.SavingsRate);

        Assert.Equal(31, summary.DaysInPeriod);
        Assert.Equal(1, summary.MonthsInPeriod);
    }

    [Fact]
    public async Task Summary_Without_Data_Returns_Null_Averages_And_No_NaN()
    {
        await using var db = await DbHelper.CreateSeededContextAsync();
        var service = new DashboardService(db);

        var summary = await service.SummarizeAsync(Query(new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 31)));

        Assert.Equal(0m, summary.TotalIncome);
        Assert.Equal(0m, summary.TotalExpenses);
        Assert.Equal(0m, summary.NetBalance);
        Assert.Null(summary.AverageExpensePerTransaction);
        Assert.Null(summary.AverageDailyExpense);
        Assert.Null(summary.AverageMonthlyExpense);
        Assert.Null(summary.SavingsRate);
        Assert.False(summary.BudgetUsage.HasBudgets);
    }

    [Fact]
    public async Task Summary_Compares_With_Previous_Equivalent_Period()
    {
        await using var db = await DbHelper.CreateSeededContextAsync();
        var service = new DashboardService(db);

        var february = await service.SummarizeAsync(Query(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)));
        var previous = february.PreviousPeriod;

        Assert.True(previous.HasComparison);
        Assert.Equal(new DateOnly(2026, 1, 4), previous.From);
        Assert.Equal(new DateOnly(2026, 1, 31), previous.To);
        Assert.Equal(1250m, previous.ExpenseDelta.Previous); // 10/01 rent, 15/01 groceries, 20/01 cinema
        Assert.Equal(-910m, previous.ExpenseDelta.AbsoluteChange);
        Assert.Equal(-72.8m, previous.ExpenseDelta.PercentChange);
        Assert.False(previous.ExpenseDelta.HigherIsBetter);

        Assert.Equal(3000m, previous.IncomeDelta.Previous); // salary on 31/01
        Assert.Equal(0m, previous.IncomeDelta.PercentChange);
        Assert.True(previous.IncomeDelta.HigherIsBetter);
    }

    [Fact]
    public async Task Summary_Percent_Change_Is_Null_Without_Previous_Data()
    {
        await using var db = await DbHelper.CreateSeededContextAsync();
        var service = new DashboardService(db);

        var january = await service.SummarizeAsync(Query(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));

        Assert.False(january.PreviousPeriod.HasComparison);
        Assert.Null(january.PreviousPeriod.IncomeDelta.PercentChange);
        Assert.Null(january.PreviousPeriod.ExpenseDelta.PercentChange);
    }

    [Fact]
    public async Task MonthlyTrends_Fills_Months_Without_Transactions()
    {
        await using var db = await DbHelper.CreateSeededContextAsync();
        var service = new DashboardService(db);

        var trends = await service.MonthlyTrendsAsync(Query(new DateOnly(2025, 12, 15), new DateOnly(2026, 2, 15)));

        Assert.Equal(3, trends.Count);
        Assert.Equal("12/2025", trends[0].Label);
        Assert.Equal(0m, trends[0].Income);
        Assert.Equal(0m, trends[0].Expenses);

        Assert.Equal("01/2026", trends[1].Label);
        Assert.Equal(3000m, trends[1].Income);
        Assert.Equal(1250m, trends[1].Expenses);
        Assert.Equal(1750m, trends[1].Balance);

        Assert.Equal("02/2026", trends[2].Label);
        Assert.Equal(-340m, trends[2].Balance);
    }

    [Fact]
    public async Task CategoryBreakdown_Uses_Expenses_By_Default()
    {
        await using var db = await DbHelper.CreateSeededContextAsync();
        var service = new DashboardService(db);

        var breakdown = await service.CategoryBreakdownAsync(Query(new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 28)));

        var top = breakdown[0];
        Assert.Equal("Housing", top.CategoryName);
        Assert.Equal(1000m, top.Amount);

        var sum = breakdown.Sum(b => b.PercentOfTotal);
        Assert.Equal(100m, Math.Round(sum, 0));
    }

    [Fact]
    public async Task WeekdaySpending_Keeps_Period_Total()
    {
        await using var db = await DbHelper.CreateSeededContextAsync();
        var service = new DashboardService(db);

        var weekdays = await service.WeekdaySpendingAsync(Query(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));

        Assert.Equal(7, weekdays.Count);
        Assert.Equal(1250m, weekdays.Sum(w => w.TotalAmount));
    }

    [Fact]
    public async Task TopExpenses_Returns_Largest_First()
    {
        await using var db = await DbHelper.CreateSeededContextAsync();
        var service = new DashboardService(db);

        var top = await service.TopExpensesAsync(Query(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)), 5);

        Assert.Equal(3, top.Count);
        Assert.Equal(1000m, top[0].Amount);
        Assert.Equal("Rent", top[0].Description);
        Assert.Equal("Housing", top[0].CategoryName);
    }

    [Fact]
    public async Task Insights_Returns_No_Data_Message_For_Empty_Period()
    {
        await using var db = await DbHelper.CreateSeededContextAsync();
        var service = new DashboardService(db);

        var insights = await service.InsightsAsync(Query(new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 31)));

        var insight = Assert.Single(insights);
        Assert.Equal("no-data", insight.Kind);
        Assert.False(insight.IsPattern);
    }

    [Fact]
    public async Task Insights_Finds_Top_Category_And_Largest_Expense()
    {
        await using var db = await DbHelper.CreateSeededContextAsync();
        var service = new DashboardService(db);

        var insights = await service.InsightsAsync(Query(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));

        Assert.Contains(insights, i => i.Kind == "top-category" && i.Value == 1000m);
        Assert.Contains(insights, i => i.Kind == "largest-expense");
        Assert.Contains(insights, i => i.Kind == "savings-rate" && i.Value == 58.33m);
    }

    [Fact]
    public async Task Insights_Flags_Budget_Exceeded()
    {
        await using var db = await DbHelper.CreateSeededContextAsync();
        db.Budgets.Add(new Budget { CategoryId = null, Year = 2026, Month = 1, LimitAmount = 1000m });
        db.Budgets.Add(new Budget { CategoryId = 2, Year = 2026, Month = 1, LimitAmount = 240m });
        await db.SaveChangesAsync();

        var service = new DashboardService(db);
        var insights = await service.InsightsAsync(Query(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));

        Assert.Contains(insights, i => i.Kind == "budget-exceeded-1"); // global
        Assert.Contains(insights, i => i.Kind == "budget-warning-2"); // groceries at 200%
    }
}
