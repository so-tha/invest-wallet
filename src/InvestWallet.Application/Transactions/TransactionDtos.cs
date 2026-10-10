using InvestWallet.Domain.Entities;
using InvestWallet.Domain.Enums;

namespace InvestWallet.Application.Transactions;

public record TransactionRequest(
    Guid AssetId,
    TransactionType Type,
    decimal Quantity,
    decimal UnitPrice,
    DateTimeOffset ExecutedAt);

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
