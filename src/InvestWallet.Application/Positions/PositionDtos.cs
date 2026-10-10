using InvestWallet.Domain.Enums;

namespace InvestWallet.Application.Positions;

/// <summary>Posição aberta de um ativo numa carteira.</summary>
/// <param name="AssetId">Ativo.</param>
/// <param name="Ticker">Ticker do ativo.</param>
/// <param name="AssetName">Nome do ativo.</param>
/// <param name="AssetType">Tipo do ativo.</param>
/// <param name="Quantity">Quantidade atualmente possuída (sempre maior que zero).</param>
/// <param name="AveragePrice">Preço médio de compra por unidade, com 6 casas decimais. Vendas não o alteram.</param>
/// <param name="TotalCost">Custo total da posição atual (quantidade × preço médio), com 6 casas decimais.</param>
public record PositionResponse(
    Guid AssetId,
    string Ticker,
    string AssetName,
    AssetType AssetType,
    decimal Quantity,
    decimal AveragePrice,
    decimal TotalCost);
