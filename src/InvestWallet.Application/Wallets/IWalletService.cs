using InvestWallet.Application.Common;

namespace InvestWallet.Application.Wallets;

public interface IWalletService
{
    Task<PagedResult<WalletResponse>> ListAsync(int page, int pageSize, CancellationToken ct);
    Task<WalletResponse> GetAsync(Guid id, CancellationToken ct);
    Task<WalletResponse> CreateAsync(WalletRequest request, CancellationToken ct);
    Task<WalletResponse> UpdateAsync(Guid id, WalletRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
