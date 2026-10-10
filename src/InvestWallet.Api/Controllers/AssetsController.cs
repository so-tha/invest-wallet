using InvestWallet.Api.Swagger;
using InvestWallet.Application.Assets;
using InvestWallet.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace InvestWallet.Api.Controllers;

/// <summary>Ativos negociáveis (ações, FIIs, ETFs, renda fixa, cripto), identificados por ticker.</summary>
[ApiController]
[Route("api/assets")]
[Produces("application/json")]
[Tags("Ativos")]
[ProducesProblemResponse(StatusCodes.Status500InternalServerError)]
public class AssetsController : ControllerBase
{
    private readonly IAssetService _service;

    public AssetsController(IAssetService service) => _service = service;

    /// <summary>Lista os ativos, ordenados por ticker.</summary>
    /// <param name="page">Número da página, começando em 1.</param>
    /// <param name="pageSize">Itens por página (padrão 20, máximo 100).</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AssetResponse>), StatusCodes.Status200OK)]
    [ProducesValidationProblemResponse]
    public Task<PagedResult<AssetResponse>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = Pagination.DefaultPageSize, CancellationToken ct = default)
        => _service.ListAsync(page, pageSize, ct);

    /// <summary>Busca um ativo pelo id.</summary>
    /// <param name="id">Id do ativo.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AssetResponse), StatusCodes.Status200OK)]
    [ProducesProblemResponse(StatusCodes.Status404NotFound)]
    public Task<AssetResponse> Get(Guid id, CancellationToken ct) => _service.GetAsync(id, ct);

    /// <summary>Cria um ativo.</summary>
    /// <remarks>O ticker é gravado em maiúsculas e sem espaços nas pontas, e é único: <c>petr4</c> e <c>PETR4</c> são o mesmo ativo.</remarks>
    /// <response code="201">Ativo criado. O cabeçalho <c>Location</c> aponta para ele.</response>
    [HttpPost]
    [ProducesResponseType(typeof(AssetResponse), StatusCodes.Status201Created)]
    [ProducesValidationProblemResponse]
    [ProducesProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AssetResponse>> Create(AssetRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Atualiza um ativo.</summary>
    /// <remarks>Manter o próprio ticker é permitido; usar o ticker de outro ativo resulta em 409.</remarks>
    /// <param name="id">Id do ativo.</param>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AssetResponse), StatusCodes.Status200OK)]
    [ProducesValidationProblemResponse]
    [ProducesProblemResponse(StatusCodes.Status404NotFound)]
    [ProducesProblemResponse(StatusCodes.Status409Conflict)]
    public Task<AssetResponse> Update(Guid id, AssetRequest request, CancellationToken ct)
        => _service.UpdateAsync(id, request, ct);

    /// <summary>Exclui um ativo.</summary>
    /// <remarks>Não é possível excluir um ativo que possui transações (409).</remarks>
    /// <param name="id">Id do ativo.</param>
    /// <response code="204">Ativo excluído.</response>
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
