using FluentValidation;
using InvestWallet.Domain.Entities;

namespace InvestWallet.Application.Assets;

public class AssetRequestValidator : AbstractValidator<AssetRequest>
{
    public AssetRequestValidator()
    {
        RuleFor(x => x.Ticker)
            .NotEmpty().WithMessage("O ticker é obrigatório.")
            .MaximumLength(Asset.TickerMaxLength).WithMessage($"O ticker deve ter no máximo {Asset.TickerMaxLength} caracteres.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome do ativo é obrigatório.")
            .MaximumLength(Asset.NameMaxLength).WithMessage($"O nome deve ter no máximo {Asset.NameMaxLength} caracteres.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Tipo de ativo inválido. Use: Stock, RealEstateFund, Etf, FixedIncome ou Crypto.");
    }
}
