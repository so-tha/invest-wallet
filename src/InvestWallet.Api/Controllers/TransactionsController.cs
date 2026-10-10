using InvestWallet.Api.Swagger;
using InvestWallet.Application.Common;
using InvestWallet.Application.Transactions;
using Microsoft.AspNetCore.Mvc;

namespace InvestWallet.Api.Controllers;

/// <summary>Compras e vendas de ativos dentro de uma carteira.</summary>
[ApiController]
[Route("api/wallets/{walletId:guid}/transactions")]
[Produces("application/json")]
[Tags("Transações")]
[ProducesProblemResponse(StatusCodes.Status500InternalServerError)]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _service;

    public TransactionsController(ITransactionService service) => _service = service;

    /// <summary>Lista as transações de uma carteira, da mais recente para a mais antiga.</summary>
    /// <param name="walletId">Id da carteira.</param>
    /// <param name="assetId">Filtra por ativo.</param>
    /// <param name="from">Inclui transações executadas a partir deste instante (ISO 8601, ex.: <c>2026-01-01T00:00:00Z</c>).</param>
    /// <param name="to">Inclui transações executadas até este instante (ISO 8601).</param>
    /// <param name="page">Número da página, começando em 1.</param>
    /// <param name="pageSize">Itens por página (padrão 20, máximo 100).</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TransactionResponse>), StatusCodes.Status200OK)]
    [ProducesValidationProblemResponse]
    [ProducesProblemResponse(StatusCodes.Status404NotFound)]
    public Task<PagedResult<TransactionResponse>> List(
        Guid walletId,
        [FromQuery] Guid? assetId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Pagination.DefaultPageSize,
        CancellationToken ct = default)
        => _service.ListAsync(walletId, assetId, from, to, page, pageSize, ct);

    /// <summary>Busca uma transação da carteira pelo id.</summary>
    /// <remarks>Uma transação só é acessível pela carteira a que pertence; por outra carteira, o resultado é 404.</remarks>
    /// <param name="walletId">Id da carteira.</param>
    /// <param name="id">Id da transação.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesProblemResponse(StatusCodes.Status404NotFound)]
    public Task<TransactionResponse> Get(Guid walletId, Guid id, CancellationToken ct)
        => _service.GetAsync(walletId, id, ct);

    /// <summary>Registra uma compra ou venda.</summary>
    /// <remarks>
    /// Regras:
    /// - quantidade e preço devem ser maiores que zero, com no máximo 12 dígitos inteiros e 6 casas decimais;
    /// - a data não pode estar no futuro; fusos horários são convertidos para UTC;
    /// - **uma venda não pode exceder a quantidade possuída na data da venda** (422). O histórico é
    ///   validado em ordem cronológica, então vender "antes" de uma compra também é recusado;
    /// - operações simultâneas sobre o mesmo ativo na mesma carteira são serializadas, então o saldo nunca fica negativo.
    ///
    /// O ativo (<c>assetId</c>) vai no corpo: se ele não existir, a resposta é 422 (a carteira, que está na URL, resulta em 404).
    /// </remarks>
    /// <param name="walletId">Id da carteira.</param>
    /// <response code="201">Transação registrada. O cabeçalho <c>Location</c> aponta para ela.</response>
    /// <response code="422">Regra de negócio violada (ex.: venda maior que a posição) ou ativo inexistente.</response>
    [HttpPost]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status201Created)]
    [ProducesValidationProblemResponse]
    [ProducesProblemResponse(StatusCodes.Status404NotFound)]
    [ProducesProblemResponse(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TransactionResponse>> Create(Guid walletId, TransactionRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(walletId, request, ct);
        return CreatedAtAction(nameof(Get), new { walletId, id = created.Id }, created);
    }

    /// <summary>Remove uma transação.</summary>
    /// <remarks>
    /// A remoção só é aceita se o histórico restante continuar válido. Por exemplo, não é possível remover uma
    /// compra que sustenta uma venda posterior (422).
    /// </remarks>
    /// <param name="walletId">Id da carteira.</param>
    /// <param name="id">Id da transação.</param>
    /// <response code="204">Transação removida.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblemResponse(StatusCodes.Status404NotFound)]
    [ProducesProblemResponse(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid walletId, Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(walletId, id, ct);
        return NoContent();
    }
}
