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
        if (quantity <= 0)
            throw new DomainException("A quantidade deve ser maior que zero.");   // RN1
        if (unitPrice <= 0)
            throw new DomainException("O preço unitário deve ser maior que zero."); // RN1

        Id = Guid.NewGuid();
        WalletId = walletId;
        AssetId = assetId;
        Type = type;
        Quantity = quantity;
        UnitPrice = unitPrice;
        ExecutedAt = executedAt;
        CreatedAt = DateTime.UtcNow;
    }
}
