using InvestWallet.Domain.Entities;

namespace InvestWallet.Application.Wallets;

public interface IWalletRepository
{
    Task<(IReadOnlyList<Wallet> Items, int TotalCount)> ListAsync(int skip, int take, CancellationToken ct);
    Task<Wallet?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<bool> HasTransactionsAsync(Guid walletId, CancellationToken ct);
    Task AddAsync(Wallet wallet, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
    Task RemoveAsync(Wallet wallet, CancellationToken ct);
}
