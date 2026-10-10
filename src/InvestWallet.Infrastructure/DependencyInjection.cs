using InvestWallet.Application.Assets;
using InvestWallet.Application.Transactions;
using InvestWallet.Application.Wallets;
using InvestWallet.Infrastructure.Persistence;
using InvestWallet.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

        // Verificação de "prontidão": executa uma consulta mínima no banco (tag "ready", usada por /health/ready)
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();

        return services;
    }

    /// <summary>
    /// Aplica as migrations pendentes. Chamado na subida da aplicação quando <c>Database:MigrateOnStartup=true</c>
    /// (veja docs/adr/0004). Seguro para uma única instância; com várias réplicas, prefira um passo de deploy separado.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Banco já está atualizado, nenhuma migration pendente");
            return;
        }

        logger.LogInformation("Aplicando {Count} migration(s): {Migrations}", pending.Count, pending);
        await db.Database.MigrateAsync(ct);
        logger.LogInformation("Migrations aplicadas com sucesso");
    }
}
