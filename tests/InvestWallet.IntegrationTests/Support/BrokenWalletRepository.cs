using InvestWallet.Application.Wallets;
using InvestWallet.Domain.Entities;

namespace InvestWallet.IntegrationTests.Support;

/// <summary>Repositório que sempre falha como um banco quebrado falharia, com um "segredo" na mensagem de erro.</summary>
public sealed class BrokenWalletRepository : IWalletRepository
{
    public const string Secret = "senha-interna-super-secreta-123";

    private static Exception Boom() => new InvalidOperationException($"falha de conexão: Password={Secret};Host=db");

    public Task<(IReadOnlyList<Wallet> Items, int TotalCount)> ListAsync(int skip, int take, CancellationToken ct) => throw Boom();
    public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken ct) => throw Boom();
    public Task<bool> HasTransactionsAsync(Guid walletId, CancellationToken ct) => throw Boom();
    public Task AddAsync(Wallet wallet, CancellationToken ct) => throw Boom();
    public Task SaveChangesAsync(CancellationToken ct) => throw Boom();
    public Task RemoveAsync(Wallet wallet, CancellationToken ct) => throw Boom();
}
