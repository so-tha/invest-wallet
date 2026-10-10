using InvestWallet.Domain.Enums;
using InvestWallet.Domain.Exceptions;

namespace InvestWallet.Domain.Entities;

public class Asset
{
    public const int TickerMaxLength = 20;
    public const int NameMaxLength = 150;

    public Guid Id { get; private set; }
    public string Ticker { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public AssetType Type { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Asset() { }

    public Asset(string ticker, string name, AssetType type)
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        Update(ticker, name, type);
    }

    public void Update(string ticker, string name, AssetType type)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            throw new DomainException("O ticker é obrigatório.");
        if (ticker.Trim().Length > TickerMaxLength)
            throw new DomainException($"O ticker deve ter no máximo {TickerMaxLength} caracteres.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("O nome do ativo é obrigatório.");
        if (name.Trim().Length > NameMaxLength)
            throw new DomainException($"O nome do ativo deve ter no máximo {NameMaxLength} caracteres.");
        if (!Enum.IsDefined(type))
            throw new DomainException("Tipo de ativo inválido.");

        Ticker = ticker.Trim().ToUpperInvariant(); // RN2: sempre maiúsculo
        Name = name.Trim();
        Type = type;
    }
}
