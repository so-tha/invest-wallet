using InvestWallet.Application.Common;

namespace InvestWallet.Application.Assets;

public interface IAssetService
{
    Task<PagedResult<AssetResponse>> ListAsync(int page, int pageSize, CancellationToken ct);
    Task<AssetResponse> GetAsync(Guid id, CancellationToken ct);
    Task<AssetResponse> CreateAsync(AssetRequest request, CancellationToken ct);
    Task<AssetResponse> UpdateAsync(Guid id, AssetRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
