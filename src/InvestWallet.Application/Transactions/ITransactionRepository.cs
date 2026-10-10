using InvestWallet.Domain.Entities;

namespace InvestWallet.Application.Transactions;

public interface ITransactionRepository
{
    Task<(IReadOnlyList<Transaction> Items, int TotalCount)> ListAsync(
        Guid walletId, TransactionFilter filter, int skip, int take, CancellationToken ct);

    /// <summary>Busca uma transação (com o ativo carregado) restrita à carteira informada.</summary>
    Task<Transaction?> GetByIdAsync(Guid walletId, Guid id, CancellationToken ct);

    /// <summary>Todas as transações de um ativo numa carteira (para calcular a posição).</summary>
    Task<IReadOnlyList<Transaction>> GetLedgerAsync(Guid walletId, Guid assetId, CancellationToken ct);

    /// <summary>Todas as transações da carteira, com o ativo carregado.</summary>
    Task<IReadOnlyList<Transaction>> GetAllByWalletAsync(Guid walletId, CancellationToken ct);

    Task AddAsync(Transaction transaction, CancellationToken ct);
    Task RemoveAsync(Transaction transaction, CancellationToken ct);

    /// <summary>
    /// Executa <paramref name="work"/> com exclusão mútua para o par (carteira, ativo), dentro de uma
    /// transação de banco. Impede que duas operações simultâneas passem pela mesma validação de saldo.
    /// </summary>
    Task<T> RunExclusiveAsync<T>(Guid walletId, Guid assetId, Func<Task<T>> work, CancellationToken ct);
}
