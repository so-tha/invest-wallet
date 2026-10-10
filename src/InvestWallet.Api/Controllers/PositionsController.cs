using InvestWallet.Application.Positions;
using Microsoft.AspNetCore.Mvc;

namespace InvestWallet.Api.Controllers;

[ApiController]
[Route("api/wallets/{walletId:guid}/positions")]
public class PositionsController : ControllerBase
{
    private readonly IPositionService _service;

    public PositionsController(IPositionService service) => _service = service;

    [HttpGet]
    public Task<IReadOnlyList<PositionResponse>> Get(Guid walletId, CancellationToken ct)
        => _service.GetByWalletAsync(walletId, ct);
}
