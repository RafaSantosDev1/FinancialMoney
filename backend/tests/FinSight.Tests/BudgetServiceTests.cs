using FinSight.Domain;
using FinSight.Dtos;
using FinSight.Services;
using Xunit;

namespace FinSight.Tests;

public class BudgetServiceTests
{
    [Fact]
    public async Task Status_Category_Budget_Without_Spending_Counts_Zero()
    {
        await using var db = await DbHelper.CreateSeededContextAsync();
        var service = new BudgetService(db);

        // Global budget for February and a housing budget for the same month,
        // but there are no housing expenses in February.
        await service.CreateAsync(new BudgetUpsertRequest(null, 2026, 2, 1000m));
        await service.CreateAsync(new BudgetUpsertRequest(1, 2026, 2, 300m));

        var status = await service.StatusAsync(2026, 2);

        var global = status.Single(s => s.CategoryId == null);
        Assert.Equal(340m, global.SpentAmount);

        var housing = status.Single(s => s.CategoryId == 1);
        Assert.Equal(0m, housing.SpentAmount);
        Assert.Equal(0m, housing.SpentPercent);
        Assert.Equal(BudgetHealth.Ok, housing.Health);
    }

    [Fact]
    public async Task Create_And_Prevent_Duplicate_Per_Month()
    {
        await using var db = await DbHelper.CreateSeededContextAsync();
        var service = new BudgetService(db);

        var created = await service.CreateAsync(new BudgetUpsertRequest(null, 2026, 1, 1500m));
        Assert.Null(created.CategoryId);
        Assert.Equal(1500m, created.LimitAmount);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.CreateAsync(new BudgetUpsertRequest(null, 2026, 1, 1600m)));
        Assert.Equal(400, exception.StatusCode);
        Assert.Contains(exception.Errors!, e => e.Contains("already exists", StringComparison.OrdinalIgnoreCase));

        // A different month or a category budget is allowed.
        await service.CreateAsync(new BudgetUpsertRequest(null, 2026, 2, 1500m));
        await service.CreateAsync(new BudgetUpsertRequest(2, 2026, 1, 500m));
    }

    [Fact]
    public async Task Status_Computes_Spent_Remaining_And_Health()
    {
        await using var db = await DbHelper.CreateSeededContextAsync();
        var service = new BudgetService(db);

        await service.CreateAsync(new BudgetUpsertRequest(null, 2026, 1, 1000m)); // global, spent 1250 -> exceeded
        await service.CreateAsync(new BudgetUpsertRequest(2, 2026, 1, 500m)); // groceries, spent 200 -> ok

        var status = await service.StatusAsync(2026, 1);
        Assert.Equal(2, status.Count);

        var global = status.Single(s => s.CategoryId == null);
        Assert.Equal(1250m, global.SpentAmount);
        Assert.Equal(-250m, global.RemainingAmount);
        Assert.Equal(125m, global.SpentPercent);
        Assert.Equal(BudgetHealth.Exceeded, global.Health);

        var groceries = status.Single(s => s.CategoryId == 2);
        Assert.Equal(200m, groceries.SpentAmount);
        Assert.Equal(300m, groceries.RemainingAmount);
        Assert.Equal(40m, groceries.SpentPercent);
        Assert.Equal(BudgetHealth.Ok, groceries.Health);
    }

    [Fact]
    public async Task Status_Warns_When_Close_To_Limit()
    {
        await using var db = await DbHelper.CreateSeededContextAsync();
        var service = new BudgetService(db);

        // Groceries spent 200 in January; 400 limit -> 50% ok, 240 limit -> 83% warning.
        await service.CreateAsync(new BudgetUpsertRequest(2, 2026, 1, 240m));

        var status = await service.StatusAsync(2026, 1);
        var groceries = Assert.Single(status);
        Assert.Equal(BudgetHealth.Warning, groceries.Health);
        Assert.Equal(83.3m, groceries.SpentPercent);
    }

    [Fact]
    public async Task Validation_Rejects_Bad_Months_And_Limits()
    {
        await using var db = await DbHelper.CreateSeededContextAsync();
        var service = new BudgetService(db);

        await Assert.ThrowsAsync<ApiException>(() =>
            service.CreateAsync(new BudgetUpsertRequest(null, 2026, 13, 100m)));
        await Assert.ThrowsAsync<ApiException>(() =>
            service.CreateAsync(new BudgetUpsertRequest(null, 2026, 1, -1m)));
        await Assert.ThrowsAsync<ApiException>(() =>
            service.CreateAsync(new BudgetUpsertRequest(999, 2026, 1, 100m)));
    }

    [Fact]
    public async Task Update_Changes_Limit()
    {
        await using var db = await DbHelper.CreateSeededContextAsync();
        var service = new BudgetService(db);

        var created = await service.CreateAsync(new BudgetUpsertRequest(null, 2026, 1, 1000m));
        var updated = await service.UpdateAsync(created.Id, new BudgetUpsertRequest(null, 2026, 1, 2000m));

        Assert.NotNull(updated);
        Assert.Equal(2000m, updated!.LimitAmount);

        Assert.True(await service.DeleteAsync(created.Id));
        Assert.Null(await service.UpdateAsync(created.Id, new BudgetUpsertRequest(null, 2026, 1, 100m)));
    }
}
