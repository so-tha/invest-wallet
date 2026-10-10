namespace InvestWallet.Domain.Enums;

/// <summary>
/// Tipo do ativo: <c>Stock</c> (ação), <c>RealEstateFund</c> (fundo imobiliário), <c>Etf</c>,
/// <c>FixedIncome</c> (renda fixa) ou <c>Crypto</c> (criptoativo).
/// </summary>
public enum AssetType
{
    Stock = 1,        // Ação
    RealEstateFund = 2, // FII
    Etf = 3,
    FixedIncome = 4,  // Renda fixa
    Crypto = 5
}
