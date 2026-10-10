using InvestWallet.Domain.Exceptions;

namespace InvestWallet.Domain.Entities;

public class Wallet
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 300;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public ICollection<Transaction> Transactions { get; private set; } = new List<Transaction>();

    private Wallet() { } // usado pelo EF Core ao ler do banco

    public Wallet(string name, string? description)
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        Update(name, description);
    }

    public void Update(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("O nome da carteira é obrigatório.");
        if (name.Trim().Length > NameMaxLength)
            throw new DomainException($"O nome da carteira deve ter no máximo {NameMaxLength} caracteres.");
        if (description is { Length: > DescriptionMaxLength })
            throw new DomainException($"A descrição deve ter no máximo {DescriptionMaxLength} caracteres.");

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
