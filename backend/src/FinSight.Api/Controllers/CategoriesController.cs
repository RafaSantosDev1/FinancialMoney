using FinSight.Dtos;
using FinSight.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController(CategoryService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> List([FromQuery] bool includeArchived = true) =>
        Ok(await service.ListAsync(includeArchived));

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(CategoryUpsertRequest request)
    {
        var created = await service.CreateAsync(request);
        return CreatedAtAction(nameof(List), new { }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryDto>> Update(int id, CategoryUpsertRequest request)
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

[ApiController]
[Route("api/accounts")]
public class AccountsController(AccountService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AccountDto>>> List([FromQuery] bool includeArchived = true) =>
        Ok(await service.ListAsync(includeArchived));

    [HttpPost]
    public async Task<ActionResult<AccountDto>> Create(AccountUpsertRequest request)
    {
        var created = await service.CreateAsync(request);
        return CreatedAtAction(nameof(List), new { }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AccountDto>> Update(int id, AccountUpsertRequest request)
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
