using InvestWallet.Application.Transactions;
using InvestWallet.Domain.Entities;

namespace InvestWallet.UnitTests.Support;

/// <summary>
/// Repositório em memória. É um FAKE (implementação simplificada que funciona de verdade), em oposição a um
/// MOCK (objeto programado para devolver respostas fixas e verificar chamadas). Aqui um fake é melhor porque
/// os testes precisam enxergar o histórico crescendo a cada transação, e programar isso com mock seria verboso.
/// </summary>
public class FakeTransactionRepository : ITransactionRepository
{
    private readonly Dictionary<Guid, Asset> _assets = new();
    public List<Transaction> Stored { get; } = new();

    public void Seed(Asset asset, params Transaction[] transactions)
    {
        _assets[asset.Id] = asset;
        foreach (var t in transactions) { Attach(t); Stored.Add(t); }
    }

    public Task<(IReadOnlyList<Transaction> Items, int TotalCount)> ListAsync(
        Guid walletId, TransactionFilter filter, int skip, int take, CancellationToken ct)
    {
        var query = Stored.Where(t => t.WalletId == walletId);
        if (filter.AssetId is not null) query = query.Where(t => t.AssetId == filter.AssetId);
        if (filter.From is not null) query = query.Where(t => t.ExecutedAt >= filter.From);
        if (filter.To is not null) query = query.Where(t => t.ExecutedAt <= filter.To);
        var all = query.OrderByDescending(t => t.ExecutedAt).ToList();
        return Task.FromResult<(IReadOnlyList<Transaction>, int)>((all.Skip(skip).Take(take).ToList(), all.Count));
    }

    public Task<Transaction?> GetByIdAsync(Guid walletId, Guid id, CancellationToken ct) =>
        Task.FromResult(Stored.FirstOrDefault(t => t.Id == id && t.WalletId == walletId));

    public Task<IReadOnlyList<Transaction>> GetLedgerAsync(Guid walletId, Guid assetId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Transaction>>(
            Stored.Where(t => t.WalletId == walletId && t.AssetId == assetId).ToList());

    public Task<IReadOnlyList<Transaction>> GetAllByWalletAsync(Guid walletId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Transaction>>(Stored.Where(t => t.WalletId == walletId).ToList());

    public Task AddAsync(Transaction transaction, CancellationToken ct)
    {
        Attach(transaction);
        Stored.Add(transaction);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Transaction transaction, CancellationToken ct)
    {
        Stored.Remove(transaction);
        return Task.CompletedTask;
    }

    // Sem concorrência nos testes unitários: basta executar o trabalho. A trava real é testada na integração.
    public Task<T> RunExclusiveAsync<T>(Guid walletId, Guid assetId, Func<Task<T>> work, CancellationToken ct) => work();

    // O EF preencheria a navegação Asset ao carregar; aqui simulamos (a propriedade tem setter privado).
    private void Attach(Transaction t)
    {
        if (_assets.TryGetValue(t.AssetId, out var asset))
            typeof(Transaction).GetProperty(nameof(Transaction.Asset))!.SetValue(t, asset);
    }
}
