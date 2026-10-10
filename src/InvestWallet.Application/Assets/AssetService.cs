using InvestWallet.Application.Common;
using InvestWallet.Application.Exceptions;
using InvestWallet.Domain.Entities;

namespace InvestWallet.Application.Assets;

public class AssetService : IAssetService
{
    private readonly IAssetRepository _assets;

    public AssetService(IAssetRepository assets) => _assets = assets;

    public async Task<PagedResult<AssetResponse>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var (items, total) = await _assets.ListAsync((page - 1) * pageSize, pageSize, ct);
        return new PagedResult<AssetResponse>(items.Select(a => a.ToResponse()).ToList(), page, pageSize, total);
    }

    public async Task<AssetResponse> GetAsync(Guid id, CancellationToken ct) =>
        (await GetOrThrowAsync(id, ct)).ToResponse();

    public async Task<AssetResponse> CreateAsync(AssetRequest request, CancellationToken ct)
    {
        // Criamos a entidade primeiro: ela normaliza o ticker (maiúsculo) e valida.
        var asset = new Asset(request.Ticker, request.Name, request.Type);
        await EnsureTickerIsFreeAsync(asset.Ticker, null, ct);
        await _assets.AddAsync(asset, ct);
        return asset.ToResponse();
    }

    public async Task<AssetResponse> UpdateAsync(Guid id, AssetRequest request, CancellationToken ct)
    {
        var asset = await GetOrThrowAsync(id, ct);
        asset.Update(request.Ticker, request.Name, request.Type);
        await EnsureTickerIsFreeAsync(asset.Ticker, id, ct);
        await _assets.SaveChangesAsync(ct);
        return asset.ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var asset = await GetOrThrowAsync(id, ct);
        if (await _assets.HasTransactionsAsync(id, ct))
            throw new ConflictException("Não é possível excluir um ativo que possui transações.");
        await _assets.RemoveAsync(asset, ct);
    }

    private async Task EnsureTickerIsFreeAsync(string ticker, Guid? excludeId, CancellationToken ct)
    {
        if (await _assets.TickerExistsAsync(ticker, excludeId, ct))
            throw new ConflictException($"Já existe um ativo com o ticker {ticker}.");
    }

    private async Task<Asset> GetOrThrowAsync(Guid id, CancellationToken ct) =>
        await _assets.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Ativo {id} não encontrado.");
}
