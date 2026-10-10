using InvestWallet.Application.Transactions;
using InvestWallet.Domain.Entities;
using InvestWallet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvestWallet.Infrastructure.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly AppDbContext _db;

    public TransactionRepository(AppDbContext db) => _db = db;

    public async Task<(IReadOnlyList<Transaction> Items, int TotalCount)> ListAsync(
        Guid walletId, TransactionFilter filter, int skip, int take, CancellationToken ct)
    {
        var query = _db.Transactions.AsNoTracking().Include(t => t.Asset).Where(t => t.WalletId == walletId);

        if (filter.AssetId is not null) query = query.Where(t => t.AssetId == filter.AssetId);
        if (filter.From is not null) query = query.Where(t => t.ExecutedAt >= filter.From);
        if (filter.To is not null) query = query.Where(t => t.ExecutedAt <= filter.To);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.ExecutedAt).ThenByDescending(t => t.CreatedAt).ThenBy(t => t.Id)
            .Skip(skip).Take(take).ToListAsync(ct);

        return (items, total);
    }

    public Task<Transaction?> GetByIdAsync(Guid walletId, Guid id, CancellationToken ct) =>
        _db.Transactions.Include(t => t.Asset)
            .FirstOrDefaultAsync(t => t.Id == id && t.WalletId == walletId, ct);

    public async Task<IReadOnlyList<Transaction>> GetLedgerAsync(Guid walletId, Guid assetId, CancellationToken ct) =>
        await _db.Transactions.AsNoTracking()
            .Where(t => t.WalletId == walletId && t.AssetId == assetId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Transaction>> GetAllByWalletAsync(Guid walletId, CancellationToken ct) =>
        await _db.Transactions.AsNoTracking().Include(t => t.Asset)
            .Where(t => t.WalletId == walletId)
            .ToListAsync(ct);

    public async Task AddAsync(Transaction transaction, CancellationToken ct)
    {
        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Transaction transaction, CancellationToken ct)
    {
        _db.Transactions.Remove(transaction);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<T> RunExclusiveAsync<T>(Guid walletId, Guid assetId, Func<Task<T>> work, CancellationToken ct)
    {
        await using var dbTransaction = await _db.Database.BeginTransactionAsync(ct);

        // Advisory lock do PostgreSQL: trava "lógica" identificada por um número (hash da chave).
        // O "xact" faz o lock ser liberado sozinho no COMMIT/ROLLBACK. Quem chegar depois com a
        // mesma chave espera aqui, e só então lê o saldo já atualizado.
        var key = $"{walletId}:{assetId}";
        await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);

        var result = await work();
        await dbTransaction.CommitAsync(ct);
        return result;
    }
}
