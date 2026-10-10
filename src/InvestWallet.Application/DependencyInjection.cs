using FluentValidation;
using InvestWallet.Application.Assets;
using InvestWallet.Application.Positions;
using InvestWallet.Application.Transactions;
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
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IPositionService, PositionService>();

        // Registra todos os AbstractValidator<> deste projeto
        services.AddValidatorsFromAssemblyContaining<WalletRequestValidator>();

        // Relógio injetável: nos testes substituímos por um relógio falso (FakeTimeProvider)
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
