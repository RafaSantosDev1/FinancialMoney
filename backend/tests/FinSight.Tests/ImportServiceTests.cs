using FinSight.Data;
using FinSight.Domain;
using FinSight.Dtos;
using FinSight.Services;
using FinSight.Services.Import;
using Xunit;

namespace FinSight.Tests;

public class ImportServiceTests
{
    private static (FinSightDbContext db, ImportService service) Create(string? databaseName = null)
    {
        var db = DbHelper.CreateContext(databaseName);
        return (db, new ImportService(db));
    }

    private static ImportPreviewRequest PreviewRequest(
        string content, ImportOptionsDto? options = null, ColumnMappingDto? mapping = null) =>
        new("transactions.csv", content, options ?? new ImportOptionsDto(null, null, null, true, null), mapping);

    private static ImportConfirmRequest ConfirmRequest(
        string content, ColumnMappingDto mapping, ImportOptionsDto? options = null, bool importDuplicates = false) =>
        new("transactions.csv", content, options ?? new ImportOptionsDto(null, null, null, true, null), mapping, importDuplicates);

    [Fact]
    public async Task Preview_Parses_Comma_Csv_With_Portuguese_Format()
    {
        var (db, service) = Create();
        var csv = "Data,Descrição,Valor\r\n10/01/2026,Supermercado Continente,\"-45,90\"\r\n05/01/2026,Salário Janeiro,\"2.450,00\"\r\n";

        var preview = await service.PreviewAsync(PreviewRequest(csv));

        Assert.Equal(',', preview.Delimiter);
        Assert.Equal("dd/MM/yyyy", preview.DetectedDateFormat);
        Assert.Equal(',', preview.DetectedDecimalSeparator);
        Assert.Equal(2, preview.Summary.TotalRows);
        Assert.Equal(2, preview.Summary.ValidRows);
        Assert.Empty(preview.Errors);

        var first = preview.Rows[0];
        Assert.Equal(new DateOnly(2026, 1, 10), first.Date);
        Assert.Equal("Supermercado Continente", first.Description);
        Assert.Equal(45.90m, first.Amount);
        Assert.Equal(TransactionType.Expense, first.Type);

        var second = preview.Rows[1];
        Assert.Equal(2450m, second.Amount);
        Assert.Equal(TransactionType.Income, second.Type);
    }

    [Fact]
    public async Task Confirm_Stores_Normalised_Transactions_In_Database()
    {
        var (db, service) = Create();
        var csv = "Data,Descrição,Valor\n10/01/2026,Supermercado,\"-45,90\"\n05/01/2026,Salário,\"2450,00\"\n";

        var preview = await service.PreviewAsync(PreviewRequest(csv));
        var result = await service.ConfirmAsync(ConfirmRequest(csv, preview.EffectiveMapping));

        Assert.Equal(2, result.ImportedRows);
        Assert.Equal(0, result.RejectedRows);

        var transactions = await service.GetBatchAsync(result.BatchId);
        Assert.NotNull(transactions);
        Assert.Equal(2, transactions!.SampleTransactions.Count);
        Assert.Contains(transactions.SampleTransactions, t => t.Amount == 45.90m && t.Type == TransactionType.Expense);
        Assert.Contains(transactions.SampleTransactions, t => t.Amount == 2450m && t.Type == TransactionType.Income);
    }

    [Fact]
    public async Task Preview_Reports_Missing_Description_As_Row_Error()
    {
        var (db, service) = Create();
        var csv = "Data,Descrição,Valor\n10/01/2026,,\"45,90\"\n12/01/2026,Padaria,\"12,00\"\n";

        var preview = await service.PreviewAsync(PreviewRequest(csv));

        Assert.Equal(1, preview.Summary.ValidRows);
        Assert.Equal(1, preview.Summary.InvalidRows);
        var error = Assert.Single(preview.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Contains(error.Errors, e => e.Contains("description", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Preview_Reports_Invalid_Dates()
    {
        var (db, service) = Create();
        var csv = "Data,Descrição,Valor\n32/01/2026,Compra,\"45,90\"\n10/01/2026,Compra,\"45,90\"\n";

        var preview = await service.PreviewAsync(PreviewRequest(csv));

        Assert.Equal(1, preview.Summary.ValidRows);
        Assert.Equal(1, preview.Summary.InvalidRows);
        Assert.Contains(Assert.Single(preview.Errors).Errors, e => e.Contains("not a valid date", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Preview_Detects_Semicolon_Delimiter()
    {
        var (db, service) = Create();
        var csv = "Data;Descrição;Valor\n10/01/2026;Padaria;12,50\n";

        var preview = await service.PreviewAsync(PreviewRequest(csv));

        Assert.Equal(';', preview.Delimiter);
        Assert.Equal(12.50m, preview.Rows[0].Amount);
    }

    [Fact]
    public async Task Preview_Parses_Thousands_Separator_And_Currency_Symbol()
    {
        var (db, service) = Create();
        var csv = "Data,Descrição,Valor\n10/01/2026,Compra,\"€ 1.234,56\"\n";

        var preview = await service.PreviewAsync(PreviewRequest(csv));

        Assert.Equal(1234.56m, preview.Rows[0].Amount);
    }

    [Fact]
    public async Task Preview_Flag_Ambiguous_Type_And_Confirm_Requires_Default()
    {
        var (db, service) = Create();
        var csv = "Data,Descrição,Valor\n10/01/2026,Padaria,\"12,00\"\n11/01/2026,Café,\"3,50\"\n";

        var preview = await service.PreviewAsync(PreviewRequest(csv));

        Assert.True(preview.AmbiguousTypeInference);
        Assert.Contains(preview.Warnings, w => w.Contains("transaction type", StringComparison.OrdinalIgnoreCase));

        await Assert.ThrowsAsync<ApiException>(() =>
            service.ConfirmAsync(ConfirmRequest(csv, preview.EffectiveMapping)));

        var options = new ImportOptionsDto(null, null, null, true, TransactionType.Expense);
        var result = await service.ConfirmAsync(ConfirmRequest(csv, preview.EffectiveMapping, options));
        Assert.Equal(2, result.ImportedRows);

        var batch = await service.GetBatchAsync(result.BatchId);
        Assert.All(batch!.SampleTransactions, t => Assert.Equal(TransactionType.Expense, t.Type));
    }

    [Fact]
    public async Task Preview_Detects_Duplicates_Inside_The_File()
    {
        var (db, service) = Create();
        var csv = "Data,Descrição,Valor\n10/01/2026,Padaria,\"-12,00\"\n10/01/2026,Padaria,\"-12,00\"\n11/01/2026,Café,\"-3,50\"\n";

        var preview = await service.PreviewAsync(PreviewRequest(csv));

        Assert.Equal(2, preview.Summary.ValidRows);
        Assert.Equal(1, preview.Summary.DuplicateRows);
        Assert.Single(preview.DuplicateGroups);

        var result = await service.ConfirmAsync(ConfirmRequest(csv, preview.EffectiveMapping));
        Assert.Equal(2, result.ImportedRows);
        Assert.Equal(1, result.DuplicateRows);

        var withDuplicates = await service.ConfirmAsync(
            ConfirmRequest(csv, preview.EffectiveMapping, importDuplicates: true));
        Assert.Equal(3, withDuplicates.ImportedRows);
    }

    [Fact]
    public async Task Preview_Detects_Duplicates_Against_Existing_Transactions()
    {
        var (db, service) = Create();
        db.Transactions.Add(new Transaction
        {
            Date = new DateOnly(2026, 1, 10),
            Description = "Padaria",
            Amount = 12.00m,
            Type = TransactionType.Expense,
            CategoryId = 1
        });
        await db.SaveChangesAsync();

        var csv = "Data,Descrição,Valor\n10/01/2026,Padaria,\"-12,00\"\n11/01/2026,Café,\"-3,50\"\n";

        var preview = await service.PreviewAsync(PreviewRequest(csv));

        Assert.Equal(1, preview.Summary.ValidRows);
        Assert.Equal(1, preview.Summary.DuplicateRows);
        Assert.Equal(1, preview.DuplicateGroups[0].ExistingMatches);
        Assert.Contains(preview.Warnings, w => w.Contains("existing transaction", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Preview_Uses_Debit_And_Credit_Columns_For_Type()
    {
        var (db, service) = Create();
        var csv = "Data,Descrição,Débito,Crédito\n10/01/2026,Loja,\"45,00\",\n05/01/2026,Empresa,,\"3000,00\"\n";

        var preview = await service.PreviewAsync(PreviewRequest(csv));

        Assert.Equal("debitCredit", preview.TypeInferenceSource);
        Assert.Equal(2, preview.Summary.ValidRows);
        var debitRow = preview.Rows.Single(r => r.Description == "Loja");
        Assert.Equal(TransactionType.Expense, debitRow.Type);
        Assert.Equal(45.00m, debitRow.DebitAmount);

        var creditRow = preview.Rows.Single(r => r.Description == "Empresa");
        Assert.Equal(TransactionType.Income, creditRow.Type);
        Assert.Equal(3000.00m, creditRow.CreditAmount);
    }

    [Fact]
    public async Task Preview_Uses_Type_Column_When_Available()
    {
        var (db, service) = Create();
        var csv = "Data,Descrição,Valor,Tipo\n10/01/2026,Padaria,\"12,00\",Despesa\n05/01/2026,Salário,\"2450,00\",Receita\n";

        var preview = await service.PreviewAsync(PreviewRequest(csv));

        Assert.Equal("typeColumn", preview.TypeInferenceSource);
        Assert.Equal(2, preview.Summary.ValidRows);
        Assert.Equal(TransactionType.Expense, preview.Rows[0].Type);
        Assert.Equal(TransactionType.Income, preview.Rows[1].Type);
    }

    [Fact]
    public async Task Preview_Recognises_Incomplete_Mapping()
    {
        var (db, service) = Create();
        var csv = "Data,Valor\n10/01/2026,45,90\n";
        var mapping = new ColumnMappingDto(DateColumn: 0, DescriptionColumn: -1, AmountColumn: 1);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.PreviewAsync(PreviewRequest(csv, mapping: mapping)));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains(exception.Errors!, e => e.Contains("description", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Preview_Preserves_Portuguese_Characters()
    {
        var (db, service) = Create();
        var csv = "Data,Descrição,Valor\n10/01/2026,Café São Miguel - açúcar,\"-4,50\"\n";

        var preview = await service.PreviewAsync(PreviewRequest(csv));

        Assert.Equal("Café São Miguel - açúcar", preview.Rows[0].Description);
    }

    [Fact]
    public async Task Preview_Rejects_Too_Many_Rows()
    {
        var (db, service) = Create();
        var sb = new System.Text.StringBuilder("Data,Descrição,Valor\n");
        for (var i = 0; i < 20_001; i++)
            sb.Append("10/01/2026,Linha,1,00\n");

        await Assert.ThrowsAsync<ApiException>(() => service.PreviewAsync(PreviewRequest(sb.ToString())));
    }

    [Fact]
    public async Task Confirm_Creates_Missing_Categories_With_Colours()
    {
        var (db, service) = Create();
        var csv = "Data,Descrição,Valor,Categoria\n10/01/2026,Padaria,\"12,00\",Padaria e Café\n";

        var preview = await service.PreviewAsync(PreviewRequest(csv));
        var options = new ImportOptionsDto(null, null, null, true, TransactionType.Expense);
        var result = await service.ConfirmAsync(ConfirmRequest(csv, preview.EffectiveMapping, options));

        Assert.Contains("Padaria e Café", result.CreatedCategories);
        var category = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Categories);
        Assert.Equal("Padaria e Café", category.Name);
        Assert.StartsWith("#", category.Color);
    }

    [Fact]
    public async Task Preview_Marks_Invalid_Amount_Rows()
    {
        var (db, service) = Create();
        var csv = "Data,Descrição,Valor\n10/01/2026,Padaria,abc\n11/01/2026,Café,\"3,50\"\n";

        var preview = await service.PreviewAsync(PreviewRequest(csv));

        Assert.Equal(1, preview.Summary.InvalidRows);
        Assert.Contains(Assert.Single(preview.Errors).Errors, e => e.Contains("not a valid amount", StringComparison.OrdinalIgnoreCase));
    }
}

