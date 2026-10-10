using InvestWallet.Application.Assets;
using InvestWallet.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace InvestWallet.Api.Controllers;

[ApiController]
[Route("api/assets")]
public class AssetsController : ControllerBase
{
    private readonly IAssetService _service;

    public AssetsController(IAssetService service) => _service = service;

    [HttpGet]
    public Task<PagedResult<AssetResponse>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = Pagination.DefaultPageSize, CancellationToken ct = default)
        => _service.ListAsync(page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<AssetResponse> Get(Guid id, CancellationToken ct) => _service.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<AssetResponse>> Create(AssetRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<AssetResponse> Update(Guid id, AssetRequest request, CancellationToken ct)
        => _service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
