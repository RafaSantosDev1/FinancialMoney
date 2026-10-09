using FinSight.Dtos;
using FinSight.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Controllers;

[ApiController]
[Route("api/budgets")]
public class BudgetsController(BudgetService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BudgetDto>>> List([FromQuery] int? year, [FromQuery] int? month) =>
        Ok(await service.ListAsync(year, month));

    [HttpGet("status")]
    public async Task<ActionResult<List<BudgetStatusDto>>> Status([FromQuery] int year, [FromQuery] int month) =>
        Ok(await service.StatusAsync(year, month));

    [HttpPost]
    public async Task<ActionResult<BudgetDto>> Create(BudgetUpsertRequest request)
    {
        var created = await service.CreateAsync(request);
        return CreatedAtAction(nameof(List), new { }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<BudgetDto>> Update(int id, BudgetUpsertRequest request)
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
