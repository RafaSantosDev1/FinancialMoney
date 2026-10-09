using FinSight.Domain;
using FinSight.Dtos;
using FinSight.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(DashboardService service) : ControllerBase
{
    private DashboardQuery Query(DateOnly from, DateOnly to, List<int>? categoryIds,
        TransactionType? type, int? accountId, string? search) => new()
        {
            From = from,
            To = to,
            CategoryIds = categoryIds,
            Type = type,
            AccountId = accountId,
            Search = search
        };

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> Summary(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] List<int>? categoryIds, [FromQuery] TransactionType? type,
        [FromQuery] int? accountId, [FromQuery] string? search)
    {
        if (to < from) throw ApiException.BadRequest("The end date must not be before the start date.");
        return Ok(await service.SummarizeAsync(Query(from, to, categoryIds, type, accountId, search)));
    }

    [HttpGet("monthly-trends")]
    public async Task<ActionResult<List<MonthlyTrendPointDto>>> MonthlyTrends(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] List<int>? categoryIds, [FromQuery] TransactionType? type,
        [FromQuery] int? accountId, [FromQuery] string? search)
    {
        if (to < from) throw ApiException.BadRequest("The end date must not be before the start date.");
        return Ok(await service.MonthlyTrendsAsync(Query(from, to, categoryIds, type, accountId, search)));
    }

    [HttpGet("category-breakdown")]
    public async Task<ActionResult<List<CategoryBreakdownItemDto>>> CategoryBreakdown(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] TransactionType? type = TransactionType.Expense,
        [FromQuery] List<int>? categoryIds = null, [FromQuery] int? accountId = null, [FromQuery] string? search = null)
    {
        if (to < from) throw ApiException.BadRequest("The end date must not be before the start date.");
        return Ok(await service.CategoryBreakdownAsync(Query(from, to, categoryIds, type, accountId, search), type ?? TransactionType.Expense));
    }

    [HttpGet("weekday-spending")]
    public async Task<ActionResult<List<WeekdaySpendingItemDto>>> WeekdaySpending(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] List<int>? categoryIds, [FromQuery] int? accountId, [FromQuery] string? search)
    {
        if (to < from) throw ApiException.BadRequest("The end date must not be before the start date.");
        return Ok(await service.WeekdaySpendingAsync(Query(from, to, categoryIds, TransactionType.Expense, accountId, search)));
    }

    [HttpGet("top-expenses")]
    public async Task<ActionResult<List<TopExpenseItemDto>>> TopExpenses(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] int count = 10,
        [FromQuery] List<int>? categoryIds = null, [FromQuery] int? accountId = null, [FromQuery] string? search = null)
    {
        if (to < from) throw ApiException.BadRequest("The end date must not be before the start date.");
        return Ok(await service.TopExpensesAsync(Query(from, to, categoryIds, TransactionType.Expense, accountId, search), count));
    }

    [HttpGet("insights")]
    public async Task<ActionResult<List<InsightDto>>> Insights(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] List<int>? categoryIds, [FromQuery] TransactionType? type,
        [FromQuery] int? accountId, [FromQuery] string? search)
    {
        if (to < from) throw ApiException.BadRequest("The end date must not be before the start date.");
        return Ok(await service.InsightsAsync(Query(from, to, categoryIds, type, accountId, search)));
    }
}
