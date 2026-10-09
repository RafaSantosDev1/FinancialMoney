using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinSight.Dtos;
using FinSight.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FinSight.Tests;

public class FinSightWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Use a dedicated in-memory database per test host so tests stay isolated.
        builder.UseSetting("FINSIGHT_INMEMORY_DB", Guid.NewGuid().ToString("N"));
    }
}

public class ApiIntegrationTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task<HttpClient> CreateClientAsync()
    {
        var factory = new FinSightWebApplicationFactory();
        return factory.CreateClient();
    }

    private static (DateOnly From, DateOnly To) CurrentMonth()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = new DateOnly(today.Year, today.Month, 1);
        return (from, from.AddMonths(1).AddDays(-1));
    }

    private static string Q(DateOnly from, DateOnly to) => $"from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";

    private static async Task<int> FirstCategoryIdAsync(HttpClient client)
    {
        var categories = await client.GetFromJsonAsync<List<CategoryDto>>("/api/categories", Json);
        Assert.NotNull(categories);
        Assert.NotEmpty(categories!);
        return categories![0].Id;
    }

    [Fact]
    public async Task Transactions_Crud_Flow()
    {
        var client = await CreateClientAsync();
        var categoryId = await FirstCategoryIdAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/transactions", new
        {
            date = "2026-01-10",
            description = "Integration purchase",
            amount = 120.50m,
            type = 1,
            categoryId,
            accountId = (int?)null,
            notes = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDto>(Json);
        Assert.NotNull(created);
        Assert.Equal(120.50m, created!.Amount);

        var getResponse = await client.GetAsync($"/api/transactions/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var updateResponse = await client.PutAsJsonAsync($"/api/transactions/{created.Id}", new
        {
            date = "2026-01-11",
            description = "Integration purchase (edited)",
            amount = 99.90m,
            type = 1,
            categoryId,
            accountId = (int?)null,
            notes = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<TransactionDto>(Json);
        Assert.Equal(99.90m, updated!.Amount);

        var deleteResponse = await client.DeleteAsync($"/api/transactions/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var missing = await client.GetAsync($"/api/transactions/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Transactions_Validation_Returns_ProblemDetails()
    {
        var client = await CreateClientAsync();
        var categoryId = await FirstCategoryIdAsync(client);

        var response = await client.PostAsJsonAsync("/api/transactions", new
        {
            date = "2026-01-10",
            description = "Invalid",
            amount = 0m,
            type = 1,
            categoryId,
            accountId = (int?)null,
            notes = (string?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.EnumerateArray().Any(e => e.GetString()!.Contains("greater than zero")));
    }

    [Fact]
    public async Task Dashboard_Summary_Reflects_Stored_Data()
    {
        var client = await CreateClientAsync();
        var categoryId = await FirstCategoryIdAsync(client);
        var (from, to) = CurrentMonth();

        await client.PostAsJsonAsync("/api/transactions", new
        {
            date = $"{from:yyyy-MM-dd}", description = "Expense one", amount = 100m, type = 1, categoryId, accountId = (int?)null, notes = (string?)null
        });
        await client.PostAsJsonAsync("/api/transactions", new
        {
            date = $"{from:yyyy-MM-dd}", description = "Expense two", amount = 50m, type = 1, categoryId, accountId = (int?)null, notes = (string?)null
        });
        await client.PostAsJsonAsync("/api/transactions", new
        {
            date = $"{to:yyyy-MM-dd}", description = "Income", amount = 1000m, type = 0, categoryId, accountId = (int?)null, notes = (string?)null
        });

        var summary = await client.GetFromJsonAsync<DashboardSummaryDto>($"/api/dashboard/summary?{Q(from, to)}", Json);

        Assert.NotNull(summary);
        Assert.Equal(150m, summary!.TotalExpenses);
        Assert.Equal(1000m, summary.TotalIncome);
        Assert.Equal(850m, summary.NetBalance);
        Assert.Equal(3, summary.TransactionCount);
        Assert.Equal(75m, summary.AverageExpensePerTransaction);
        Assert.Equal(85m, summary.SavingsRate);
    }

    [Fact]
    public async Task Dashboard_Monthly_Trends_Return_Every_Month_In_Range()
    {
        var client = await CreateClientAsync();
        var categoryId = await FirstCategoryIdAsync(client);

        await client.PostAsJsonAsync("/api/transactions", new
        {
            date = "2026-01-15", description = "Rent", amount = 500m, type = 1, categoryId, accountId = (int?)null, notes = (string?)null
        });

        var trends = await client.GetFromJsonAsync<List<MonthlyTrendPointDto>>(
            "/api/dashboard/monthly-trends?from=2025-12-01&to=2026-02-28", Json);

        Assert.NotNull(trends);
        Assert.Equal(3, trends!.Count);
        Assert.Equal(0m, trends[0].Expenses);
        Assert.Equal(500m, trends[1].Expenses);
        Assert.Equal(0m, trends[2].Expenses);
    }

    [Fact]
    public async Task Import_Preview_And_Confirm_End_To_End()
    {
        var client = await CreateClientAsync();

        var csv = "Data,Descrição,Valor\n10/01/2026,Supermercado,\"-45,90\"\n05/01/2026,Salário,\"2450,00\"\n";

        var previewResponse = await client.PostAsJsonAsync("/api/imports/preview", new
        {
            fileName = "bank.csv",
            content = csv,
            options = new { delimiter = (char?)null, dateFormat = (string?)null, decimalSeparator = (char?)null, hasHeader = true, defaultType = (int?)null },
            mapping = (ColumnMappingDto?)null
        });
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);

        var preview = await previewResponse.Content.ReadFromJsonAsync<ImportPreviewResponse>(Json);
        Assert.NotNull(preview);
        Assert.Equal(2, preview!.Summary.ValidRows);

        var confirmResponse = await client.PostAsJsonAsync("/api/imports/confirm", new
        {
            fileName = "bank.csv",
            content = csv,
            options = new { delimiter = (char?)null, dateFormat = (string?)null, decimalSeparator = (char?)null, hasHeader = true, defaultType = (int?)null },
            mapping = preview.EffectiveMapping,
            importDuplicates = false
        });
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        var result = await confirmResponse.Content.ReadFromJsonAsync<ImportResultDto>(Json);
        Assert.NotNull(result);
        Assert.Equal(2, result!.ImportedRows);
        Assert.Equal(0, result.RejectedRows);

        var batch = await client.GetFromJsonAsync<ImportBatchDto>($"/api/imports/{result.BatchId}", Json);
        Assert.NotNull(batch);
        Assert.Equal(2, batch!.ImportedRows);
    }

    [Fact]
    public async Task Seed_Demo_Data_Can_Be_Created_And_Removed()
    {
        var client = await CreateClientAsync();

        var status = await client.GetFromJsonAsync<SeedStatusDto>("/api/seed/status", Json);
        Assert.False(status!.HasDemoData);

        var seeded = await client.PostAsync("/api/seed/demo", null);
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        var seedResult = await seeded.Content.ReadFromJsonAsync<SeedResultDto>(Json);
        Assert.True(seedResult!.Transactions > 10);

        var statusAfterSeed = await client.GetFromJsonAsync<SeedStatusDto>("/api/seed/status", Json);
        Assert.True(statusAfterSeed!.HasDemoData);
        Assert.True(statusAfterSeed.DemoCategories > 5);

        var removed = await client.DeleteAsync("/api/seed/demo");
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);

        var statusAfterRemoval = await client.GetFromJsonAsync<SeedStatusDto>("/api/seed/status", Json);
        Assert.False(statusAfterRemoval!.HasDemoData);
    }

    [Fact]
    public async Task Budgets_Status_Reports_Spending_Against_Limits()
    {
        var client = await CreateClientAsync();
        var categories = await client.GetFromJsonAsync<List<CategoryDto>>("/api/categories", Json);
        var categoryId = categories![0].Id;
        var (from, to) = CurrentMonth();

        await client.PostAsJsonAsync("/api/transactions", new
        {
            date = $"{from:yyyy-MM-dd}", description = "Rent", amount = 800m, type = 1, categoryId, accountId = (int?)null, notes = (string?)null
        });

        var budgetResponse = await client.PostAsJsonAsync("/api/budgets", new
        {
            categoryId = (int?)categoryId,
            year = from.Year,
            month = from.Month,
            limitAmount = 500m
        });
        Assert.Equal(HttpStatusCode.Created, budgetResponse.StatusCode);

        var status = await client.GetFromJsonAsync<List<BudgetStatusDto>>(
            $"/api/budgets/status?year={from.Year}&month={from.Month}", Json);

        Assert.NotNull(status);
        var budget = Assert.Single(status!);
        Assert.Equal(800m, budget.SpentAmount);
        Assert.Equal(160m, budget.SpentPercent);
        Assert.Equal(BudgetHealth.Exceeded, budget.Health);
    }
}
