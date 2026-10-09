using FinSight.Domain;
using FinSight.Dtos;
using FinSight.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Controllers;

[ApiController]
[Route("api/transactions")]
public class TransactionsController(TransactionService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<TransactionDto>>> List(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] List<int>? categoryIds, [FromQuery] TransactionType? type,
        [FromQuery] int? accountId, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string sortBy = "date", [FromQuery] bool descending = true)
    {
        var query = new TransactionQuery
        {
            From = from, To = to, CategoryIds = categoryIds, Type = type, AccountId = accountId,
            Search = search, Page = page, PageSize = pageSize, SortBy = sortBy, Descending = descending
        };
        return Ok(await service.QueryAsync(query));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TransactionDto>> Get(int id)
    {
        var transaction = await service.GetAsync(id);
        return transaction is null ? NotFound() : Ok(transaction);
    }

    [HttpPost]
    public async Task<ActionResult<TransactionDto>> Create(TransactionCreateRequest request)
    {
        var created = await service.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TransactionDto>> Update(int id, TransactionUpdateRequest request)
    {
        var updated = await service.UpdateAsync(id, request);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        return await service.DeleteAsync(id) ? NoContent() : NotFound();
    }
}
