using InvestWallet.Domain.Entities;
using InvestWallet.Domain.Enums;

namespace InvestWallet.Application.Assets;

/// <summary>Dados para criar ou atualizar um ativo.</summary>
/// <param name="Ticker">Código de negociação (obrigatório, até 20 caracteres). Gravado em maiúsculas e único.</param>
/// <param name="Name">Nome do ativo (obrigatório, até 150 caracteres).</param>
/// <param name="Type">Tipo do ativo: Stock (ação), RealEstateFund (fundo imobiliário), Etf, FixedIncome (renda fixa) ou Crypto.</param>
public record AssetRequest(string Ticker, string Name, AssetType Type);

/// <summary>Ativo negociável.</summary>
/// <param name="Id">Identificador único.</param>
/// <param name="Ticker">Código de negociação, em maiúsculas.</param>
/// <param name="Name">Nome do ativo.</param>
/// <param name="Type">Tipo do ativo.</param>
/// <param name="CreatedAt">Instante de criação, em UTC.</param>
public record AssetResponse(Guid Id, string Ticker, string Name, AssetType Type, DateTime CreatedAt);

public static class AssetMappings
{
    public static AssetResponse ToResponse(this Asset a) =>
        new(a.Id, a.Ticker, a.Name, a.Type, a.CreatedAt);
}
