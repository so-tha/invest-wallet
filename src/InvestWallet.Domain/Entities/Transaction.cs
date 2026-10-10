using InvestWallet.Domain.Enums;
using InvestWallet.Domain.Exceptions;

namespace InvestWallet.Domain.Entities;

public class Transaction
{
    public Guid Id { get; private set; }
    public Guid WalletId { get; private set; }
    public Guid AssetId { get; private set; }
    public TransactionType Type { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public DateTime ExecutedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Wallet? Wallet { get; private set; }
    public Asset? Asset { get; private set; }

    private Transaction() { }

    public Transaction(Guid walletId, Guid assetId, TransactionType type,
                       decimal quantity, decimal unitPrice, DateTime executedAt)
    {
        if (!Enum.IsDefined(type))
            throw new DomainException("Tipo de transação inválido.");
        ValidateAmount(quantity, "A quantidade");   // RN1
        ValidateAmount(unitPrice, "O preço unitário");

        Id = Guid.NewGuid();
        WalletId = walletId;
        AssetId = assetId;
        Type = type;
        Quantity = quantity;
        UnitPrice = unitPrice;
        ExecutedAt = executedAt;
        CreatedAt = DateTime.UtcNow;
    }

    // numeric(18,6) no banco: até 6 casas decimais e menos de 1 trilhão.
    // Validar aqui evita arredondamento silencioso e erro de overflow do banco.
    private const decimal MaxAmount = 999_999_999_999m;

    private static void ValidateAmount(decimal value, string label)
    {
        if (value <= 0)
            throw new DomainException($"{label} deve ser maior que zero.");
        if (value > MaxAmount)
            throw new DomainException($"{label} excede o valor máximo permitido.");
        if (decimal.Round(value, 6) != value)
            throw new DomainException($"{label} deve ter no máximo 6 casas decimais.");
    }
}
