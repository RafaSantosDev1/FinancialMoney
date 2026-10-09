using FinSight.Domain;
using FinSight.Dtos;
using FinSight.Services;
using Xunit;

namespace FinSight.Tests;

public class TransactionServiceTests
{
    [Fact]
    public async Task Query_Paginates_And_Filters()
    {
        await using var db = await DbHelper.CreateSeededContextAsync();
        var service = new TransactionService(db);

        var page = await service.QueryAsync(new TransactionQuery { Page = 1, PageSize = 3, SortBy = "date", Descending = true });

        Assert.Equal(7, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(3, page.Items.Count);
        Assert.Equal(new DateOnly(2026, 2, 28), page.Items[0].Date);

        var expenses = await service.QueryAsync(new TransactionQuery { Type = TransactionType.Expense });
        Assert.Equal(5, expenses.TotalCount);

        var groceries = await service.QueryAsync(new TransactionQuery { CategoryIds = [2] });
        Assert.Equal(2, groceries.TotalCount);

        var search = await service.QueryAsync(new TransactionQuery { Search = "cinema" });
        Assert.Equal(1, search.TotalCount);
    }

    [Fact]
    public async Task Create_Then_Update_Then_Delete()
    {
        await using var db = await DbHelper.CreateSeededContextAsync();
        var service = new TransactionService(db);

        var created = await service.CreateAsync(new TransactionCreateRequest(
            new DateOnly(2026, 3, 5), "New purchase", 12.5m, TransactionType.Expense, 1, 1, "note"));

        Assert.True(created.Id > 0);
        Assert.Equal("Housing", created.CategoryName);
        Assert.Equal("Main account", created.AccountName);

        var updated = await service.UpdateAsync(created.Id, new TransactionUpdateRequest(
            new DateOnly(2026, 3, 6), "Updated purchase", 99.9m, TransactionType.Expense, 2, 1, null));

        Assert.NotNull(updated);
        Assert.Equal("Updated purchase", updated!.Description);
        Assert.Equal(99.9m, updated.Amount);

        Assert.True(await service.DeleteAsync(created.Id));
        Assert.False(await service.DeleteAsync(created.Id));
        Assert.Null(await service.GetAsync(created.Id));
    }

    [Fact]
    public async Task Create_Rejects_Invalid_Values()
    {
        await using var db = await DbHelper.CreateSeededContextAsync();
        var service = new TransactionService(db);

        await Assert.ThrowsAsync<ApiException>(() => service.CreateAsync(new TransactionCreateRequest(
            new DateOnly(2026, 3, 5), "Zero", 0m, TransactionType.Expense, 1, null, null)));

        await Assert.ThrowsAsync<ApiException>(() => service.CreateAsync(new TransactionCreateRequest(
            new DateOnly(2026, 3, 5), "Unknown category", 10m, TransactionType.Expense, 999, null, null)));

        await Assert.ThrowsAsync<ApiException>(() => service.CreateAsync(new TransactionCreateRequest(
            new DateOnly(2026, 3, 5), "Unknown account", 10m, TransactionType.Expense, 1, 999, null)));

        await Assert.ThrowsAsync<ApiException>(() => service.CreateAsync(new TransactionCreateRequest(
            new DateOnly(2026, 3, 5), "   ", 10m, TransactionType.Expense, 1, null, null)));
    }

    [Fact]
    public async Task Create_Rejects_Archived_Category()
    {
        await using var db = await DbHelper.CreateSeededContextAsync();
        var category = new Category { Name = "Archived", IsArchived = true };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var service = new TransactionService(db);
        await Assert.ThrowsAsync<ApiException>(() => service.CreateAsync(new TransactionCreateRequest(
            new DateOnly(2026, 3, 5), "Too late", 10m, TransactionType.Expense, category.Id, null, null)));
    }
}
