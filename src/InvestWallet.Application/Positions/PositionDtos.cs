using InvestWallet.Domain.Enums;

namespace InvestWallet.Application.Positions;

public record PositionResponse(
    Guid AssetId,
    string Ticker,
    string AssetName,
    AssetType AssetType,
    decimal Quantity,
    decimal AveragePrice,
    decimal TotalCost);
