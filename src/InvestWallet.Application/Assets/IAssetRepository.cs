using InvestWallet.Domain.Entities;

namespace InvestWallet.Application.Assets;

public interface IAssetRepository
{
    Task<(IReadOnlyList<Asset> Items, int TotalCount)> ListAsync(int skip, int take, CancellationToken ct);
    Task<Asset?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<bool> TickerExistsAsync(string ticker, Guid? excludeId, CancellationToken ct);
    Task<bool> HasTransactionsAsync(Guid assetId, CancellationToken ct);
    Task AddAsync(Asset asset, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
    Task RemoveAsync(Asset asset, CancellationToken ct);
}
