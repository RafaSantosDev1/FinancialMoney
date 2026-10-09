using FinSight.Dtos;
using FinSight.Services;
using FinSight.Services.Import;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Controllers;

[ApiController]
[Route("api/imports")]
public class ImportsController(ImportService service) : ControllerBase
{
    [HttpPost("preview")]
    public async Task<ActionResult<ImportPreviewResponse>> Preview(ImportPreviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw ApiException.BadRequest("The file content is required.");
        return Ok(await service.PreviewAsync(request));
    }

    [HttpPost("confirm")]
    public async Task<ActionResult<ImportResultDto>> Confirm(ImportConfirmRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw ApiException.BadRequest("The file content is required.");
        return Ok(await service.ConfirmAsync(request));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ImportBatchDto>> Get(int id)
    {
        var batch = await service.GetBatchAsync(id);
        return batch is null ? NotFound() : Ok(batch);
    }
}
