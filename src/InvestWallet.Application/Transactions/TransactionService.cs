using InvestWallet.Application.Assets;
using InvestWallet.Application.Common;
using InvestWallet.Application.Exceptions;
using InvestWallet.Application.Wallets;
using InvestWallet.Domain.Entities;
using InvestWallet.Domain.Exceptions;
using InvestWallet.Domain.Positions;

namespace InvestWallet.Application.Transactions;

public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _transactions;
    private readonly IWalletRepository _wallets;
    private readonly IAssetRepository _assets;
    private readonly TimeProvider _clock;

    public TransactionService(
        ITransactionRepository transactions, IWalletRepository wallets, IAssetRepository assets, TimeProvider clock)
    {
        _transactions = transactions;
        _wallets = wallets;
        _assets = assets;
        _clock = clock;
    }

    public async Task<PagedResult<TransactionResponse>> ListAsync(
        Guid walletId, Guid? assetId, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize, CancellationToken ct)
    {
        await EnsureWalletExistsAsync(walletId, ct);
        (page, pageSize) = Pagination.Normalize(page, pageSize);

        var filter = new TransactionFilter(assetId, from?.UtcDateTime, to?.UtcDateTime);
        var (items, total) = await _transactions.ListAsync(walletId, filter, (page - 1) * pageSize, pageSize, ct);

        return new PagedResult<TransactionResponse>(
            items.Select(t => t.ToResponse(t.Asset!.Ticker)).ToList(), page, pageSize, total);
    }

    public async Task<TransactionResponse> GetAsync(Guid walletId, Guid id, CancellationToken ct)
    {
        await EnsureWalletExistsAsync(walletId, ct);
        var transaction = await GetOrThrowAsync(walletId, id, ct);
        return transaction.ToResponse(transaction.Asset!.Ticker);
    }

    public async Task<TransactionResponse> CreateAsync(Guid walletId, TransactionRequest request, CancellationToken ct)
    {
        await EnsureWalletExistsAsync(walletId, ct);

        // O ativo vem no corpo (não na URL): se não existe, a requisição é bem formada mas inprocessável (422).
        var asset = await _assets.GetByIdAsync(request.AssetId, ct)
            ?? throw new DomainException($"Ativo {request.AssetId} não encontrado.");

        var executedAt = request.ExecutedAt.UtcDateTime; // Npgsql exige UTC em timestamptz
        if (executedAt > _clock.GetUtcNow().UtcDateTime)
            throw new DomainException("A data da transação não pode estar no futuro."); // RN7

        var transaction = new Transaction(walletId, asset.Id, request.Type, request.Quantity, request.UnitPrice, executedAt);

        await _transactions.RunExclusiveAsync(walletId, asset.Id, async () =>
        {
            // RN3: valida o histórico inteiro COM a nova transação. Lança se algum ponto ficar negativo.
            var ledger = await _transactions.GetLedgerAsync(walletId, asset.Id, ct);
            PositionCalculator.Calculate(asset.Id, ledger.Append(transaction));

            await _transactions.AddAsync(transaction, ct);
            return true;
        }, ct);

        return transaction.ToResponse(asset.Ticker);
    }

    public async Task DeleteAsync(Guid walletId, Guid id, CancellationToken ct)
    {
        await EnsureWalletExistsAsync(walletId, ct);
        var transaction = await GetOrThrowAsync(walletId, id, ct);

        await _transactions.RunExclusiveAsync(walletId, transaction.AssetId, async () =>
        {
            // RN6: o histórico SEM esta transação ainda precisa ser válido
            // (ex.: remover uma compra que sustentava uma venda posterior).
            var ledger = await _transactions.GetLedgerAsync(walletId, transaction.AssetId, ct);
            PositionCalculator.Calculate(transaction.AssetId, ledger.Where(t => t.Id != id));

            await _transactions.RemoveAsync(transaction, ct);
            return true;
        }, ct);
    }

    private async Task EnsureWalletExistsAsync(Guid walletId, CancellationToken ct)
    {
        if (await _wallets.GetByIdAsync(walletId, ct) is null)
            throw new NotFoundException($"Carteira {walletId} não encontrada.");
    }

    private async Task<Transaction> GetOrThrowAsync(Guid walletId, Guid id, CancellationToken ct) =>
        await _transactions.GetByIdAsync(walletId, id, ct)
            ?? throw new NotFoundException($"Transação {id} não encontrada nesta carteira.");
}
