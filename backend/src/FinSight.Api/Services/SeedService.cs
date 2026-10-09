using FinSight.Data;
using FinSight.Domain;
using FinSight.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Services;

public class SeedService(FinSightDbContext db)
{
    public const string DemoBatchName = "demo-seed";

    private static readonly (string Name, string Color, CategoryAppliesTo AppliesTo)[] DefaultCategories =
    [
        ("Housing", "#38bdf8", CategoryAppliesTo.Expense),
        ("Groceries", "#34d399", CategoryAppliesTo.Expense),
        ("Transport", "#fbbf24", CategoryAppliesTo.Expense),
        ("Health", "#fb7185", CategoryAppliesTo.Expense),
        ("Education", "#818cf8", CategoryAppliesTo.Expense),
        ("Leisure", "#a78bfa", CategoryAppliesTo.Expense),
        ("Subscriptions", "#f472b6", CategoryAppliesTo.Expense),
        ("Shopping", "#2dd4bf", CategoryAppliesTo.Expense),
        ("Travel", "#fb923c", CategoryAppliesTo.Expense),
        ("Salary", "#4ade80", CategoryAppliesTo.Income),
        ("Other", "#94a3b8", CategoryAppliesTo.Both)
    ];

    public async Task<int> EnsureDefaultCategoriesAsync(CancellationToken ct = default)
    {
        var existing = await db.Categories.AsNoTracking().Select(c => c.Name).ToListAsync(ct);
        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var (name, color, appliesTo) in DefaultCategories)
        {
            if (existingSet.Contains(name)) continue;
            db.Categories.Add(new Category { Name = name, Color = color, AppliesTo = appliesTo });
            added++;
        }
        if (added > 0) await db.SaveChangesAsync(ct);
        return added;
    }

    public async Task<SeedStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var hasDemo = await db.Accounts.AnyAsync(a => a.IsDemo, ct);
        return new SeedStatusDto(
            hasDemo,
            hasDemo ? await db.Categories.CountAsync(c => c.IsDemo, ct) : 0,
            hasDemo ? await db.Budgets.CountAsync(b => b.IsDemo, ct) : 0,
            hasDemo ? await CountDemoTransactionsAsync(ct) : 0);
    }

    private async Task<int> CountDemoTransactionsAsync(CancellationToken ct) =>
        await db.Transactions.CountAsync(t =>
            (t.Category != null && t.Category.IsDemo) ||
            (t.Account != null && t.Account.IsDemo) ||
            (t.ImportBatch != null && t.ImportBatch.FileName == DemoBatchName), ct);

    public async Task<SeedResultDto> SeedDemoAsync(CancellationToken ct = default)
    {
        await ResetDemoAsync(ct);

        var random = new Random(20261009);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-5);

        // Demo categories (recognisable by the "Demo " prefix and removable).
        var categories = DefaultCategories
            .Select(c => new Category
            {
                Name = $"Demo {c.Name}",
                Color = c.Color,
                AppliesTo = c.AppliesTo,
                IsDemo = true
            })
            .ToList();
        db.Categories.AddRange(categories);

        var account = new Account { Name = "Demo Account", AccountType = AccountType.Checking, IsDemo = true };
        db.Accounts.Add(account);

        // Persist categories/accounts first so that IDs are available for the transactions.
        if (db.Database.IsRelational())
        {
            await db.SaveChangesAsync(ct);
        }

        Category Find(string name) => categories.First(c => c.Name == $"Demo {name}");

        var batch = new ImportBatch
        {
            FileName = DemoBatchName,
            ImportedAt = DateTime.UtcNow,
            TotalRows = 0
        };

        var transactions = new List<Transaction>();
        var month = firstMonth;
        while (month <= new DateOnly(today.Year, today.Month, 1))
        {
            var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
            var lastDay = month.Month == today.Month ? today.Day : daysInMonth;

            // Salary - two pay days per month
            foreach (var day in new[] { 1, 15 })
            {
                if (day > lastDay) continue;
                transactions.Add(new Transaction
                {
                    Date = new DateOnly(month.Year, month.Month, day),
                    Description = "Monthly salary",
                    Amount = 2450m + random.Next(-120, 160),
                    Type = TransactionType.Income,
                    CategoryId = Find("Salary").Id,
                    Account = account,
                    ImportBatch = batch
                });
            }

            // Occasional freelance income
            if (month.Month % 3 == 0 && lastDay >= 20)
            {
                transactions.Add(new Transaction
                {
                    Date = new DateOnly(month.Year, month.Month, 20),
                    Description = "Freelance project",
                    Amount = 320m + random.Next(-80, 240),
                    Type = TransactionType.Income,
                    CategoryId = Find("Other").Id,
                    Account = account,
                    ImportBatch = batch
                });
            }

            // Recurring subscriptions
            foreach (var (name, day, amount) in new[]
                     {
                         ("Netflix subscription", 5, 13.99m),
                         ("Spotify Premium", 8, 10.99m),
                         ("Gym membership", 12, 39.90m),
                         ("Internet provider", 18, 44.99m)
                     })
            {
                if (day > lastDay) continue;
                transactions.Add(new Transaction
                {
                    Date = new DateOnly(month.Year, month.Month, day),
                    Description = name,
                    Amount = amount,
                    Type = TransactionType.Expense,
                    CategoryId = Find("Subscriptions").Id,
                    Account = account,
                    ImportBatch = batch
                });
            }

            // Rent
            transactions.Add(new Transaction
            {
                Date = new DateOnly(month.Year, month.Month, 3),
                Description = "Rent payment",
                Amount = 980m,
                Type = TransactionType.Expense,
                CategoryId = Find("Housing").Id,
                Account = account,
                ImportBatch = batch
            });

            // Utilities
            transactions.Add(new Transaction
            {
                Date = new DateOnly(month.Year, month.Month, 9),
                Description = "Electricity and water",
                Amount = 85m + random.Next(-25, 60),
                Type = TransactionType.Expense,
                CategoryId = Find("Housing").Id,
                Account = account,
                ImportBatch = batch
            });

            // Random groceries
            var groceryCount = random.Next(4, 7);
            for (var i = 0; i < groceryCount; i++)
            {
                var day = random.Next(1, lastDay + 1);
                transactions.Add(new Transaction
                {
                    Date = new DateOnly(month.Year, month.Month, day),
                    Description = random.Next(2) == 0 ? "Supermarket" : "Local grocery store",
                    Amount = Math.Round(25m + (decimal)random.NextDouble() * 90m, 2),
                    Type = TransactionType.Expense,
                    CategoryId = Find("Groceries").Id,
                    Account = account,
                    ImportBatch = batch
                });
            }

            // Transport
            var transportCount = random.Next(2, 5);
            for (var i = 0; i < transportCount; i++)
            {
                var day = random.Next(1, lastDay + 1);
                transactions.Add(new Transaction
                {
                    Date = new DateOnly(month.Year, month.Month, day),
                    Description = random.Next(3) switch { 0 => "Metro pass", 1 => "Fuel", _ => "Rideshare" },
                    Amount = Math.Round(12m + (decimal)random.NextDouble() * 55m, 2),
                    Type = TransactionType.Expense,
                    CategoryId = Find("Transport").Id,
                    Account = account,
                    ImportBatch = batch
                });
            }

            // Leisure / shopping / health / education - occasional
            foreach (var (category, description, min, max, probability) in new[]
                     {
                         ("Leisure", "Cinema tickets", 15m, 45m, 0.5),
                         ("Shopping", "Online purchase", 20m, 180m, 0.6),
                         ("Health", "Pharmacy", 10m, 60m, 0.35),
                         ("Education", "Online course", 30m, 120m, 0.25)
                     })
            {
                if (random.NextDouble() < probability)
                {
                    var day = random.Next(1, lastDay + 1);
                    transactions.Add(new Transaction
                    {
                        Date = new DateOnly(month.Year, month.Month, day),
                        Description = description,
                        Amount = Math.Round(min + (decimal)random.NextDouble() * (max - min), 2),
                        Type = TransactionType.Expense,
                        CategoryId = Find(category).Id,
                        Account = account,
                        ImportBatch = batch
                    });
                }
            }

            // One weekend trip in two of the months
            if (month.Month % 5 == 2 && lastDay >= 14)
            {
                transactions.Add(new Transaction
                {
                    Date = new DateOnly(month.Year, month.Month, 14),
                    Description = "Weekend trip hotel",
                    Amount = 180m + random.Next(-40, 220),
                    Type = TransactionType.Expense,
                    CategoryId = Find("Travel").Id,
                    Account = account,
                    ImportBatch = batch
                });
            }

            month = month.AddMonths(1);
        }

        batch.TotalRows = transactions.Count;
        batch.ImportedRows = transactions.Count;
        db.ImportBatches.Add(batch);
        db.Transactions.AddRange(transactions);

        // Demo budgets for the current month (global + two categories)
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        var budgetStart = currentMonth.AddMonths(-5);
        var budgets = new List<Budget>();
        var m = budgetStart;
        while (m <= currentMonth)
        {
            budgets.Add(new Budget
            {
                CategoryId = null,
                Year = m.Year,
                Month = m.Month,
                LimitAmount = 3200m,
                IsDemo = true
            });
            budgets.Add(new Budget
            {
                CategoryId = Find("Groceries").Id,
                Year = m.Year,
                Month = m.Month,
                LimitAmount = 550m,
                IsDemo = true
            });
            budgets.Add(new Budget
            {
                CategoryId = Find("Leisure").Id,
                Year = m.Year,
                Month = m.Month,
                LimitAmount = 200m,
                IsDemo = true
            });
            m = m.AddMonths(1);
        }
        db.Budgets.AddRange(budgets);

        await db.SaveChangesAsync(ct);

        return new SeedResultDto(
            categories.Count, budgets.Count, transactions.Count,
            firstMonth, new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)));
    }

    public async Task<int> ResetDemoAsync(CancellationToken ct = default)
    {
        // Everything is removed through the change tracker so EF can order the
        // DELETEs correctly (transactions reference demo categories with Restrict).
        var transactions = await db.Transactions
            .Where(t =>
                (t.Category != null && t.Category.IsDemo) ||
                (t.Account != null && t.Account.IsDemo) ||
                (t.ImportBatch != null && t.ImportBatch.FileName == DemoBatchName))
            .ToListAsync(ct);
        var removed = transactions.Count;

        var batches = await db.ImportBatches.Where(b => b.FileName == DemoBatchName).ToListAsync(ct);
        var demoBudgets = await db.Budgets.Where(b => b.IsDemo).ToListAsync(ct);
        var demoAccounts = await db.Accounts.Where(a => a.IsDemo).ToListAsync(ct);
        var demoCategories = await db.Categories.Where(c => c.IsDemo).ToListAsync(ct);

        db.Transactions.RemoveRange(transactions);
        db.ImportBatches.RemoveRange(batches);
        db.Budgets.RemoveRange(demoBudgets);
        db.Accounts.RemoveRange(demoAccounts);
        db.Categories.RemoveRange(demoCategories);

        await db.SaveChangesAsync(ct);
        return removed;
    }
}

public record SeedStatusDto(bool HasDemoData, int DemoCategories, int DemoBudgets, int DemoTransactions);

public record SeedResultDto(int Categories, int Budgets, int Transactions, DateOnly From, DateOnly To);

