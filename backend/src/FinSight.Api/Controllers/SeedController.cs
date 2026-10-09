using FinSight.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Controllers;

[ApiController]
[Route("api/seed")]
public class SeedController(SeedService service) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<SeedStatusDto>> Status() => Ok(await service.GetStatusAsync());

    /// <summary>Creates a full, clearly marked demo dataset (categories, accounts, budgets, transactions).</summary>
    [HttpPost("demo")]
    public async Task<ActionResult<SeedResultDto>> SeedDemo() => Ok(await service.SeedDemoAsync());

    /// <summary>Removes all demo data previously created by the seed endpoint.</summary>
    [HttpDelete("demo")]
    public async Task<ActionResult<int>> RemoveDemo() => Ok(await service.ResetDemoAsync());
}
