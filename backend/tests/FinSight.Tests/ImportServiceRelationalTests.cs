using FinSight.Data;
using FinSight.Dtos;
using FinSight.Services;
using FinSight.Services.Import;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinSight.Tests;

/// <summary>
/// Relational tests (SQLite in-memory) that catch problems the in-memory provider
/// hides, such as foreign keys pointing at not-yet-saved entities.
/// </summary>
public class ImportServiceRelationalTests
{
    private static (FinSightDbContext Db, ImportService Service) CreateSqlite()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<FinSightDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new FinSightDbContext(options);
        db.Database.EnsureCreated();
        return (db, new ImportService(db));
    }

    [Fact]
    public async Task Confirm_Links_Transactions_To_Auto_Created_Categories()
    {
        var (db, service) = CreateSqlite();
        await using var _ = db;

        var csv = "Data,Descrição,Valor\n10/01/2026,Padaria,\"-12,00\"\n11/01/2026,Café,\"-3,50\"\n";
        var preview = await service.PreviewAsync(new ImportPreviewRequest(
            "x.csv", csv, new ImportOptionsDto(null, null, null, true, null), null));

        Assert.Equal(2, preview.Summary.ValidRows);

        var result = await service.ConfirmAsync(new ImportConfirmRequest(
            "x.csv", csv, new ImportOptionsDto(null, null, null, true, null), preview.EffectiveMapping, false));

        Assert.Equal(2, result.ImportedRows);
        Assert.Contains("Outros", result.CreatedCategories);

        var category = await db.Categories.SingleAsync();
        Assert.True(category.Id > 0);

        var transactions = await db.Transactions.ToListAsync();
        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, t => Assert.Equal(category.Id, t.CategoryId));
    }

    [Fact]
    public async Task Demo_Data_Can_Be_Seeded_And_Removed_Repeatedly()
    {
        var (db, _) = CreateSqlite();
        await using var __ = db;
        var seed = new SeedService(db);

        for (var cycle = 0; cycle < 2; cycle++)
        {
            var result = await seed.SeedDemoAsync();
            Assert.True(result.Transactions > 10);

            var status = await seed.GetStatusAsync();
            Assert.True(status.HasDemoData);
            Assert.True(status.DemoCategories > 5);
            Assert.True(status.DemoTransactions > 10);
            Assert.Equal(0, await db.Transactions.CountAsync(t => t.Category != null && t.Category.IsDemo && t.CategoryId == 0));

            var removed = await seed.ResetDemoAsync();
            Assert.Equal(result.Transactions, removed);

            var after = await seed.GetStatusAsync();
            Assert.False(after.HasDemoData);
            Assert.Equal(0, await db.Categories.CountAsync(c => c.IsDemo));
            Assert.Equal(0, await db.Accounts.CountAsync(a => a.IsDemo));
            Assert.Equal(0, await db.Budgets.CountAsync(b => b.IsDemo));
            Assert.Equal(0, await db.Transactions.CountAsync());
            Assert.Equal(0, await db.ImportBatches.CountAsync(b => b.FileName == "demo-seed"));
        }
    }

    [Fact]
    public async Task Confirm_Uses_Transaction_To_Import_Rows()
    {
        var (db, service) = CreateSqlite();
        await using var _ = db;

        db.Categories.Add(new Domain.Category { Id = 1, Name = "Outros", Color = "#38bdf8" });
        await db.SaveChangesAsync();

        var csv = "Data,Descrição,Valor\n10/01/2026,Padaria,\"-12,00\"\n";
        var preview = await service.PreviewAsync(new ImportPreviewRequest(
            "y.csv", csv, new ImportOptionsDto(null, null, null, true, null), null));
        var result = await service.ConfirmAsync(new ImportConfirmRequest(
            "y.csv", csv, new ImportOptionsDto(null, null, null, true, null), preview.EffectiveMapping, false));

        Assert.Equal(1, result.ImportedRows);
        Assert.Empty(result.CreatedCategories);

        var batch = await db.ImportBatches.Include(b => b.Transactions).SingleAsync();
        var imported = Assert.Single(batch.Transactions);
        Assert.Equal(12.00m, imported.Amount);
        Assert.Equal(1, imported.CategoryId);
        Assert.Equal(batch.Id, imported.ImportBatchId);
    }

    [Fact]
    public async Task Confirm_Rolls_Back_When_A_Row_Fails()
    {
        var (db, service) = CreateSqlite();
        await using var _ = db;

        // The second row has an invalid date, so only the first one can be imported
        // while the batch still reports the rejection transparently.
        var csv = "Data,Descrição,Valor\n10/01/2026,Padaria,\"-12,00\"\n31/31/2026,Café,\"-3,50\"\n";
        var preview = await service.PreviewAsync(new ImportPreviewRequest(
            "z.csv", csv, new ImportOptionsDto(null, null, null, true, null), null));

        Assert.Equal(1, preview.Summary.ValidRows);
        Assert.Equal(1, preview.Summary.InvalidRows);

        var result = await service.ConfirmAsync(new ImportConfirmRequest(
            "z.csv", csv, new ImportOptionsDto(null, null, null, true, null), preview.EffectiveMapping, false));

        Assert.Equal(1, result.ImportedRows);
        Assert.Equal(1, result.RejectedRows);
        Assert.Single(result.RejectedRowsDetail);
    }
}
