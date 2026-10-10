using InvestWallet.Domain.Entities;
using InvestWallet.Domain.Enums;

namespace InvestWallet.Application.Transactions;

/// <summary>Dados de uma compra ou venda.</summary>
/// <param name="AssetId">Id do ativo negociado (deve existir).</param>
/// <param name="Type">Buy (compra) ou Sell (venda).</param>
/// <param name="Quantity">Quantidade negociada. Maior que zero; até 12 dígitos inteiros e 6 casas decimais.</param>
/// <param name="UnitPrice">Preço por unidade. Maior que zero; até 12 dígitos inteiros e 6 casas decimais.</param>
/// <param name="ExecutedAt">Instante da negociação (ISO 8601, com ou sem fuso; convertido para UTC). Não pode estar no futuro.</param>
public record TransactionRequest(
    Guid AssetId,
    TransactionType Type,
    decimal Quantity,
    decimal UnitPrice,
    DateTimeOffset ExecutedAt);

/// <summary>Compra ou venda registrada.</summary>
/// <param name="Id">Identificador único.</param>
/// <param name="WalletId">Carteira à qual a transação pertence.</param>
/// <param name="AssetId">Ativo negociado.</param>
/// <param name="AssetTicker">Ticker do ativo, para conveniência.</param>
/// <param name="Type">Buy (compra) ou Sell (venda).</param>
/// <param name="Quantity">Quantidade negociada.</param>
/// <param name="UnitPrice">Preço por unidade.</param>
/// <param name="TotalValue">Quantidade × preço unitário.</param>
/// <param name="ExecutedAt">Instante da negociação, em UTC.</param>
/// <param name="CreatedAt">Instante em que a transação foi registrada, em UTC.</param>
public record TransactionResponse(
    Guid Id,
    Guid WalletId,
    Guid AssetId,
    string AssetTicker,
    TransactionType Type,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalValue,
    DateTime ExecutedAt,
    DateTime CreatedAt);

/// <summary>Filtros opcionais da listagem. Datas já convertidas para UTC.</summary>
public record TransactionFilter(Guid? AssetId, DateTime? From, DateTime? To);

public static class TransactionMappings
{
    public static TransactionResponse ToResponse(this Transaction t, string assetTicker) =>
        new(t.Id, t.WalletId, t.AssetId, assetTicker, t.Type, t.Quantity, t.UnitPrice,
            t.Quantity * t.UnitPrice, t.ExecutedAt, t.CreatedAt);
}
