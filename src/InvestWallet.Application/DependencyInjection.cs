using InvestWallet.Application.Assets;
using InvestWallet.Application.Wallets;
using Microsoft.Extensions.DependencyInjection;

namespace InvestWallet.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Scoped = uma instância por requisição HTTP
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IAssetService, AssetService>();
        return services;
    }
}
