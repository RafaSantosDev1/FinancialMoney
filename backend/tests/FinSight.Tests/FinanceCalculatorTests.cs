using FinSight.Domain;
using FinSight.Services;
using Xunit;

namespace FinSight.Tests;

public class FinanceCalculatorTests
{
    private static Transaction Tx(DateOnly date, decimal amount, TransactionType type, int categoryId = 1, string description = "Test") =>
        new() { Date = date, Amount = amount, Type = type, CategoryId = categoryId, Description = description };

    [Fact]
    public void TotalIncome_And_Expenses_Use_Signed_Amounts_By_Type()
    {
        var transactions = new[]
        {
            Tx(new DateOnly(2026, 1, 1), 100m, TransactionType.Income, 3, "Salary"),
            Tx(new DateOnly(2026, 1, 2), 40m, TransactionType.Expense),
            Tx(new DateOnly(2026, 1, 3), 60m, TransactionType.Expense),
            Tx(new DateOnly(2026, 1, 4), 25m, TransactionType.Income, 3, "Bonus")
        };

        Assert.Equal(125m, FinanceCalculator.TotalIncome(transactions));
        Assert.Equal(100m, FinanceCalculator.TotalExpenses(transactions));
        Assert.Equal(25m, FinanceCalculator.NetBalance(125m, 100m));
    }

    [Fact]
    public void AverageExpensePerTransaction_Is_Null_Without_Expenses()
    {
        Assert.Null(FinanceCalculator.AverageExpensePerTransaction(0m, 0));
        Assert.Equal(50m, FinanceCalculator.AverageExpensePerTransaction(100m, 2));
    }

    [Fact]
    public void AverageDailyExpense_Uses_Calendar_Days_Including_Days_Without_Transactions()
    {
        // 100 EUR spread over a 10 day period, only 2 days with transactions.
        Assert.Equal(10m, FinanceCalculator.AverageDailyExpense(100m, 10));
        Assert.Null(FinanceCalculator.AverageDailyExpense(100m, 0));
    }

    [Fact]
    public void AverageMonthlyExpense_Uses_Number_Of_Months_Touched_By_Period()
    {
        Assert.Equal(300m, FinanceCalculator.AverageMonthlyExpense(900m, 3));
        Assert.Null(FinanceCalculator.AverageMonthlyExpense(900m, 0));
    }

    [Fact]
    public void SavingsRate_Is_Null_Without_Income()
    {
        Assert.Null(FinanceCalculator.SavingsRate(50m, 0m));
        Assert.Equal(20m, FinanceCalculator.SavingsRate(200m, 1000m));
        Assert.Equal(-10m, FinanceCalculator.SavingsRate(-100m, 1000m));
    }

    [Fact]
    public void PercentChange_Is_Null_When_Previous_Value_Is_Zero()
    {
        Assert.Null(FinanceCalculator.PercentChange(100m, 0m));
        Assert.Equal(50m, FinanceCalculator.PercentChange(150m, 100m));
        Assert.Equal(-50m, FinanceCalculator.PercentChange(50m, 100m));
        Assert.Equal(200m, FinanceCalculator.PercentChange(50m, -50m));
    }

    [Fact]
    public void Delta_Encodes_Direction_Expectation()
    {
        var expenseDelta = FinanceCalculator.Delta(80m, 100m, higherIsBetter: false);
        Assert.Equal(-20m, expenseDelta.AbsoluteChange);
        Assert.Equal(-20m, expenseDelta.PercentChange);
        Assert.False(expenseDelta.HigherIsBetter);

        var incomeDelta = FinanceCalculator.Delta(80m, 100m, higherIsBetter: true);
        Assert.Equal(-20m, incomeDelta.AbsoluteChange);
        Assert.True(incomeDelta.HigherIsBetter);
    }

    [Fact]
    public void Days_In_Range_Are_Inclusive()
    {
        Assert.Equal(31, FinanceCalculator.DaysInRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));
        Assert.Equal(1, FinanceCalculator.DaysInRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1)));
        Assert.Equal(0, FinanceCalculator.DaysInRange(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Months_In_Range_Count_Distinct_Calendar_Months()
    {
        Assert.Equal(1, FinanceCalculator.MonthsInRange(new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 31)));
        Assert.Equal(3, FinanceCalculator.MonthsInRange(new DateOnly(2026, 1, 15), new DateOnly(2026, 3, 2)));
        Assert.Equal(12, FinanceCalculator.MonthsInRange(new DateOnly(2025, 3, 1), new DateOnly(2026, 2, 28)));
    }

    [Fact]
    public void PreviousPeriod_Has_Same_Length_And_Ends_Day_Before_Current()
    {
        var (from, to) = FinanceCalculator.PreviousPeriod(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));
        Assert.Equal(new DateOnly(2026, 1, 4), from);
        Assert.Equal(new DateOnly(2026, 1, 31), to);

        var (f2, t2) = FinanceCalculator.PreviousPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        Assert.Equal(new DateOnly(2025, 12, 1), f2);
        Assert.Equal(new DateOnly(2025, 12, 31), t2);
    }
}
