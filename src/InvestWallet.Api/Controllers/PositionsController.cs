using InvestWallet.Api.Swagger;
using InvestWallet.Application.Positions;
using Microsoft.AspNetCore.Mvc;

namespace InvestWallet.Api.Controllers;

/// <summary>Posição atual de uma carteira, calculada a partir do histórico de transações.</summary>
[ApiController]
[Route("api/wallets/{walletId:guid}/positions")]
[Produces("application/json")]
[Tags("Posições")]
[ProducesProblemResponse(StatusCodes.Status500InternalServerError)]
public class PositionsController : ControllerBase
{
    private readonly IPositionService _service;

    public PositionsController(IPositionService service) => _service = service;

    /// <summary>Posição por ativo: quantidade, preço médio e custo total.</summary>
    /// <remarks>
    /// A posição **não é armazenada**: é recalculada do histórico a cada consulta, então nunca diverge das transações.
    /// O preço médio considera apenas as compras; vendas reduzem a quantidade sem alterá-lo.
    /// Só aparecem posições abertas (quantidade maior que zero), ordenadas por ticker.
    /// </remarks>
    /// <param name="walletId">Id da carteira.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PositionResponse>), StatusCodes.Status200OK)]
    [ProducesProblemResponse(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<PositionResponse>> Get(Guid walletId, CancellationToken ct)
        => _service.GetByWalletAsync(walletId, ct);
}
