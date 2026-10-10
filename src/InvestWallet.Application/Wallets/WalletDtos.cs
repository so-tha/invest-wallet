using InvestWallet.Domain.Entities;

namespace InvestWallet.Application.Wallets;

public record WalletRequest(string Name, string? Description);

public record WalletResponse(Guid Id, string Name, string? Description, DateTime CreatedAt);

public static class WalletMappings
{
    public static WalletResponse ToResponse(this Wallet w) =>
        new(w.Id, w.Name, w.Description, w.CreatedAt);
}
