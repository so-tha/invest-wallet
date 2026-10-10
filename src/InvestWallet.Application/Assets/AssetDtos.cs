using InvestWallet.Domain.Entities;
using InvestWallet.Domain.Enums;

namespace InvestWallet.Application.Assets;

public record AssetRequest(string Ticker, string Name, AssetType Type);

public record AssetResponse(Guid Id, string Ticker, string Name, AssetType Type, DateTime CreatedAt);

public static class AssetMappings
{
    public static AssetResponse ToResponse(this Asset a) =>
        new(a.Id, a.Ticker, a.Name, a.Type, a.CreatedAt);
}
