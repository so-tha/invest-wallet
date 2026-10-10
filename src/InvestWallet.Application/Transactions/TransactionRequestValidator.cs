using FluentValidation;

namespace InvestWallet.Application.Transactions;

public class TransactionRequestValidator : AbstractValidator<TransactionRequest>
{
    public TransactionRequestValidator(TimeProvider clock)
    {
        RuleFor(x => x.AssetId)
            .NotEmpty().WithMessage("O ativo é obrigatório.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Tipo de transação inválido. Use Buy ou Sell.");

        // numeric(18,6) no banco = até 12 dígitos inteiros e 6 casas decimais
        RuleFor(x => x.Quantity).Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage("A quantidade deve ser maior que zero.")
            .PrecisionScale(18, 6, false).WithMessage("A quantidade aceita no máximo 12 dígitos inteiros e 6 casas decimais.");

        RuleFor(x => x.UnitPrice).Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage("O preço unitário deve ser maior que zero.")
            .PrecisionScale(18, 6, false).WithMessage("O preço unitário aceita no máximo 12 dígitos inteiros e 6 casas decimais.");

        // RN7: dá para decidir só com a requisição e o relógio, sem consultar o banco → é validação (400)
        RuleFor(x => x.ExecutedAt).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("A data da transação é obrigatória.")
            .Must(date => date <= clock.GetUtcNow()).WithMessage("A data da transação não pode estar no futuro.");
    }
}
