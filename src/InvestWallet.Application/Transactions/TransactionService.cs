using FluentValidation;
using Microsoft.Extensions.Logging;
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
    private readonly IValidator<TransactionRequest> _validator;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(
        ITransactionRepository transactions, IWalletRepository wallets, IAssetRepository assets,
        IValidator<TransactionRequest> validator, ILogger<TransactionService> logger)
    {
        _transactions = transactions;
        _wallets = wallets;
        _assets = assets;
        _validator = validator;
        _logger = logger;
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
        await _validator.ValidateAndThrowAsync(request, ct); // 400 antes de gastar consulta ao banco
        await EnsureWalletExistsAsync(walletId, ct);

        // O ativo vem no corpo (não na URL): se não existe, a requisição é bem formada mas inprocessável (422).
        var asset = await _assets.GetByIdAsync(request.AssetId, ct)
            ?? throw new DomainException($"Ativo {request.AssetId} não encontrado.");

        var executedAt = request.ExecutedAt.UtcDateTime; // Npgsql exige UTC em timestamptz

        var transaction = new Transaction(walletId, asset.Id, request.Type, request.Quantity, request.UnitPrice, executedAt);

        await _transactions.RunExclusiveAsync(walletId, asset.Id, async () =>
        {
            // RN3: valida o histórico inteiro COM a nova transação. Lança se algum ponto ficar negativo.
            var ledger = await _transactions.GetLedgerAsync(walletId, asset.Id, ct);
            PositionCalculator.Calculate(asset.Id, ledger.Append(transaction));

            await _transactions.AddAsync(transaction, ct);
            return true;
        }, ct);

        // Template estruturado: cada {Campo} vira uma propriedade pesquisável no log (não é concatenação de texto)
        _logger.LogInformation(
            "Transação {TransactionId} registrada: {Type} {Quantity} de {Ticker} na carteira {WalletId}",
            transaction.Id, transaction.Type, transaction.Quantity, asset.Ticker, walletId);

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

        _logger.LogInformation("Transação {TransactionId} removida da carteira {WalletId}", id, walletId);
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
