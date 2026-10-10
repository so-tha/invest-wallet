using FluentValidation;
using InvestWallet.Domain.Entities;

namespace InvestWallet.Application.Wallets;

public class WalletRequestValidator : AbstractValidator<WalletRequest>
{
    public WalletRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome da carteira é obrigatório.")
            .MaximumLength(Wallet.NameMaxLength).WithMessage($"O nome deve ter no máximo {Wallet.NameMaxLength} caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(Wallet.DescriptionMaxLength).WithMessage($"A descrição deve ter no máximo {Wallet.DescriptionMaxLength} caracteres.");
    }
}
