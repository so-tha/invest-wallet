using InvestWallet.Api.Swagger;
using InvestWallet.Application.Common;
using InvestWallet.Application.Wallets;
using Microsoft.AspNetCore.Mvc;

namespace InvestWallet.Api.Controllers;

/// <summary>Carteiras de investimento.</summary>
[ApiController]
[Route("api/wallets")]
[Produces("application/json")]
[Tags("Carteiras")]
[ProducesProblemResponse(StatusCodes.Status500InternalServerError)]
public class WalletsController : ControllerBase
{
    private readonly IWalletService _service;

    public WalletsController(IWalletService service) => _service = service;

    /// <summary>Lista as carteiras, ordenadas por nome.</summary>
    /// <param name="page">Número da página, começando em 1.</param>
    /// <param name="pageSize">Itens por página (padrão 20, máximo 100; valores maiores são reduzidos a 100).</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<WalletResponse>), StatusCodes.Status200OK)]
    [ProducesValidationProblemResponse]
    public Task<PagedResult<WalletResponse>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = Pagination.DefaultPageSize, CancellationToken ct = default)
        => _service.ListAsync(page, pageSize, ct);

    /// <summary>Busca uma carteira pelo id.</summary>
    /// <param name="id">Id da carteira.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WalletResponse), StatusCodes.Status200OK)]
    [ProducesProblemResponse(StatusCodes.Status404NotFound)]
    public Task<WalletResponse> Get(Guid id, CancellationToken ct) => _service.GetAsync(id, ct);

    /// <summary>Cria uma carteira.</summary>
    /// <remarks>Espaços nas pontas do nome e da descrição são removidos. Uma descrição em branco é gravada como nula.</remarks>
    /// <response code="201">Carteira criada. O cabeçalho <c>Location</c> aponta para ela.</response>
    [HttpPost]
    [ProducesResponseType(typeof(WalletResponse), StatusCodes.Status201Created)]
    [ProducesValidationProblemResponse]
    public async Task<ActionResult<WalletResponse>> Create(WalletRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Atualiza nome e descrição de uma carteira.</summary>
    /// <param name="id">Id da carteira.</param>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(WalletResponse), StatusCodes.Status200OK)]
    [ProducesValidationProblemResponse]
    [ProducesProblemResponse(StatusCodes.Status404NotFound)]
    public Task<WalletResponse> Update(Guid id, WalletRequest request, CancellationToken ct)
        => _service.UpdateAsync(id, request, ct);

    /// <summary>Exclui uma carteira.</summary>
    /// <remarks>Não é possível excluir uma carteira que possui transações (409).</remarks>
    /// <param name="id">Id da carteira.</param>
    /// <response code="204">Carteira excluída.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblemResponse(StatusCodes.Status404NotFound)]
    [ProducesProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
