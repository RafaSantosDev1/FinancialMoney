using FinSight.Data;
using FinSight.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Tests;

public static class DbHelper
{
    public static FinSightDbContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<FinSightDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString("N"))
            .EnableSensitiveDataLogging(false)
            .Options;
        return new FinSightDbContext(options);
    }

    public static async Task<FinSightDbContext> CreateSeededContextAsync(string? databaseName = null)
    {
        var db = CreateContext(databaseName);
        var categories = new List<Category>
        {
            new() { Id = 1, Name = "Housing", Color = "#38bdf8" },
            new() { Id = 2, Name = "Groceries", Color = "#34d399" },
            new() { Id = 3, Name = "Salary", Color = "#4ade80", AppliesTo = CategoryAppliesTo.Income },
            new() { Id = 4, Name = "Leisure", Color = "#a78bfa" }
        };
        db.Categories.AddRange(categories);

        var account = new Account { Id = 1, Name = "Main account" };
        db.Accounts.Add(account);

        var transactions = new List<Transaction>
        {
            new() { Date = new DateOnly(2026, 1, 10), Description = "Rent", Amount = 1000m, Type = TransactionType.Expense, CategoryId = 1, AccountId = 1 },
            new() { Date = new DateOnly(2026, 1, 15), Description = "Supermarket", Amount = 200m, Type = TransactionType.Expense, CategoryId = 2, AccountId = 1 },
            new() { Date = new DateOnly(2026, 1, 20), Description = "Cinema", Amount = 50m, Type = TransactionType.Expense, CategoryId = 4, AccountId = 1 },
            new() { Date = new DateOnly(2026, 1, 31), Description = "January salary", Amount = 3000m, Type = TransactionType.Income, CategoryId = 3, AccountId = 1 },
            new() { Date = new DateOnly(2026, 2, 5), Description = "Supermarket", Amount = 250m, Type = TransactionType.Expense, CategoryId = 2, AccountId = 1 },
            new() { Date = new DateOnly(2026, 2, 15), Description = "Concert", Amount = 90m, Type = TransactionType.Expense, CategoryId = 4, AccountId = 1 },
            new() { Date = new DateOnly(2026, 2, 28), Description = "February salary", Amount = 3000m, Type = TransactionType.Income, CategoryId = 3, AccountId = 1 }
        };
        db.Transactions.AddRange(transactions);

        await db.SaveChangesAsync();
        return db;
    }
}
