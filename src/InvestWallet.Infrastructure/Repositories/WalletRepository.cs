using InvestWallet.Application.Wallets;
using InvestWallet.Domain.Entities;
using InvestWallet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvestWallet.Infrastructure.Repositories;

public class WalletRepository : IWalletRepository
{
    private readonly AppDbContext _db;

    public WalletRepository(AppDbContext db) => _db = db;

    public async Task<(IReadOnlyList<Wallet> Items, int TotalCount)> ListAsync(int skip, int take, CancellationToken ct)
    {
        var query = _db.Wallets.AsNoTracking(); // só leitura: não precisa rastrear mudanças
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(w => w.Name).ThenBy(w => w.Id)
                               .Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.Wallets.FirstOrDefaultAsync(w => w.Id == id, ct);

    public Task<bool> HasTransactionsAsync(Guid walletId, CancellationToken ct) =>
        _db.Transactions.AnyAsync(t => t.WalletId == walletId, ct);

    public async Task AddAsync(Wallet wallet, CancellationToken ct)
    {
        _db.Wallets.Add(wallet);
        await _db.SaveChangesAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

    public async Task RemoveAsync(Wallet wallet, CancellationToken ct)
    {
        _db.Wallets.Remove(wallet);
        await _db.SaveChangesAsync(ct);
    }
}
