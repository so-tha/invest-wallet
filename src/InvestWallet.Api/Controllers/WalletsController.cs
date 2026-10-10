using InvestWallet.Application.Common;
using InvestWallet.Application.Wallets;
using Microsoft.AspNetCore.Mvc;

namespace InvestWallet.Api.Controllers;

[ApiController]
[Route("api/wallets")]
public class WalletsController : ControllerBase
{
    private readonly IWalletService _service;

    public WalletsController(IWalletService service) => _service = service;

    [HttpGet]
    public Task<PagedResult<WalletResponse>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = Pagination.DefaultPageSize, CancellationToken ct = default)
        => _service.ListAsync(page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<WalletResponse> Get(Guid id, CancellationToken ct) => _service.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<WalletResponse>> Create(WalletRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<WalletResponse> Update(Guid id, WalletRequest request, CancellationToken ct)
        => _service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
