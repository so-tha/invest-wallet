using InvestWallet.Application.Assets;
using InvestWallet.Domain.Entities;
using InvestWallet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvestWallet.Infrastructure.Repositories;

public class AssetRepository : IAssetRepository
{
    private readonly AppDbContext _db;

    public AssetRepository(AppDbContext db) => _db = db;

    public async Task<(IReadOnlyList<Asset> Items, int TotalCount)> ListAsync(int skip, int take, CancellationToken ct)
    {
        var query = _db.Assets.AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(a => a.Ticker)
                               .Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    public Task<Asset?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.Assets.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<bool> TickerExistsAsync(string ticker, Guid? excludeId, CancellationToken ct) =>
        _db.Assets.AnyAsync(a => a.Ticker == ticker && (excludeId == null || a.Id != excludeId), ct);

    public Task<bool> HasTransactionsAsync(Guid assetId, CancellationToken ct) =>
        _db.Transactions.AnyAsync(t => t.AssetId == assetId, ct);

    public async Task AddAsync(Asset asset, CancellationToken ct)
    {
        _db.Assets.Add(asset);
        await _db.SaveChangesAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

    public async Task RemoveAsync(Asset asset, CancellationToken ct)
    {
        _db.Assets.Remove(asset);
        await _db.SaveChangesAsync(ct);
    }
}
