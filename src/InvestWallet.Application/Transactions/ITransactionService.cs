using InvestWallet.Application.Common;

namespace InvestWallet.Application.Transactions;

public interface ITransactionService
{
    Task<PagedResult<TransactionResponse>> ListAsync(
        Guid walletId, Guid? assetId, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize, CancellationToken ct);
    Task<TransactionResponse> GetAsync(Guid walletId, Guid id, CancellationToken ct);
    Task<TransactionResponse> CreateAsync(Guid walletId, TransactionRequest request, CancellationToken ct);
    Task DeleteAsync(Guid walletId, Guid id, CancellationToken ct);
}
