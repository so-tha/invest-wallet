using InvestWallet.Domain.Entities;

namespace InvestWallet.Application.Wallets;

/// <summary>Dados para criar ou atualizar uma carteira.</summary>
/// <param name="Name">Nome da carteira (obrigatório, até 100 caracteres). Espaços nas pontas são removidos.</param>
/// <param name="Description">Descrição opcional (até 300 caracteres). Em branco é gravada como nula.</param>
public record WalletRequest(string Name, string? Description);

/// <summary>Carteira de investimentos.</summary>
/// <param name="Id">Identificador único.</param>
/// <param name="Name">Nome da carteira.</param>
/// <param name="Description">Descrição, ou nulo se não houver.</param>
/// <param name="CreatedAt">Instante de criação, em UTC.</param>
public record WalletResponse(Guid Id, string Name, string? Description, DateTime CreatedAt);

public static class WalletMappings
{
    public static WalletResponse ToResponse(this Wallet w) =>
        new(w.Id, w.Name, w.Description, w.CreatedAt);
}
