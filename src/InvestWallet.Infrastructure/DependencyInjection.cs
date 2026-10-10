using InvestWallet.Application.Assets;
using InvestWallet.Application.Transactions;
using InvestWallet.Application.Wallets;
using InvestWallet.Infrastructure.Persistence;
using InvestWallet.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvestWallet.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Connection string 'Default' não configurada. Defina a variável de ambiente ConnectionStrings__Default (veja .env.example).");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention()); // Wallet.CreatedAt -> created_at

        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();

        return services;
    }
}
