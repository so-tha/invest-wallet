using InvestWallet.Application.Common;
using InvestWallet.Application.Transactions;
using Microsoft.AspNetCore.Mvc;

namespace InvestWallet.Api.Controllers;

[ApiController]
[Route("api/wallets/{walletId:guid}/transactions")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _service;

    public TransactionsController(ITransactionService service) => _service = service;

    [HttpGet]
    public Task<PagedResult<TransactionResponse>> List(
        Guid walletId,
        [FromQuery] Guid? assetId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Pagination.DefaultPageSize,
        CancellationToken ct = default)
        => _service.ListAsync(walletId, assetId, from, to, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<TransactionResponse> Get(Guid walletId, Guid id, CancellationToken ct)
        => _service.GetAsync(walletId, id, ct);

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> Create(Guid walletId, TransactionRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(walletId, request, ct);
        return CreatedAtAction(nameof(Get), new { walletId, id = created.Id }, created);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid walletId, Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(walletId, id, ct);
        return NoContent();
    }
}
