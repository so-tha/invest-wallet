using FluentValidation;
using InvestWallet.Application.Common;
using InvestWallet.Application.Exceptions;
using InvestWallet.Domain.Entities;

namespace InvestWallet.Application.Wallets;

public class WalletService : IWalletService
{
    private readonly IWalletRepository _wallets;
    private readonly IValidator<WalletRequest> _validator;

    public WalletService(IWalletRepository wallets, IValidator<WalletRequest> validator)
    {
        _wallets = wallets;
        _validator = validator;
    }

    public async Task<PagedResult<WalletResponse>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var (items, total) = await _wallets.ListAsync((page - 1) * pageSize, pageSize, ct);
        return new PagedResult<WalletResponse>(items.Select(w => w.ToResponse()).ToList(), page, pageSize, total);
    }

    public async Task<WalletResponse> GetAsync(Guid id, CancellationToken ct) =>
        (await GetOrThrowAsync(id, ct)).ToResponse();

    public async Task<WalletResponse> CreateAsync(WalletRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        var wallet = new Wallet(request.Name, request.Description);
        await _wallets.AddAsync(wallet, ct);
        return wallet.ToResponse();
    }

    public async Task<WalletResponse> UpdateAsync(Guid id, WalletRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        var wallet = await GetOrThrowAsync(id, ct);
        wallet.Update(request.Name, request.Description);
        await _wallets.SaveChangesAsync(ct);
        return wallet.ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var wallet = await GetOrThrowAsync(id, ct);
        if (await _wallets.HasTransactionsAsync(id, ct))
            throw new ConflictException("Não é possível excluir uma carteira que possui transações.");
        await _wallets.RemoveAsync(wallet, ct);
    }

    private async Task<Wallet> GetOrThrowAsync(Guid id, CancellationToken ct) =>
        await _wallets.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Carteira {id} não encontrada.");
}
