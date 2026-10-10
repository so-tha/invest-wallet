namespace InvestWallet.Domain.Positions;

/// <summary>Posição de um ativo numa carteira: quanto se tem e quanto custou (em média).</summary>
public record Position(Guid AssetId, decimal Quantity, decimal TotalCost)
{
    public decimal AveragePrice => Quantity == 0 ? 0 : TotalCost / Quantity;
}
