using InvestWallet.Domain.Entities;
using InvestWallet.Domain.Enums;
using InvestWallet.Domain.Exceptions;

namespace InvestWallet.Domain.Positions;

/// <summary>
/// Calcula a posição de UM ativo a partir do histórico de transações (método do preço médio).
/// Também é o guardião das regras RN3 e RN6: se em algum ponto da história a quantidade ficaria
/// negativa, lança <see cref="DomainException"/>.
/// </summary>
public static class PositionCalculator
{
    public static Position Calculate(Guid assetId, IEnumerable<Transaction> transactions)
    {
        decimal quantity = 0;
        decimal totalCost = 0;

        // A ordem cronológica importa: vender antes de comprar é inválido, mesmo que o total feche.
        // Desempate por CreatedAt e Id para o resultado ser sempre o mesmo (determinístico).
        var ordered = transactions
            .OrderBy(t => t.ExecutedAt)
            .ThenBy(t => t.CreatedAt)
            .ThenBy(t => t.Id);

        foreach (var t in ordered)
        {
            if (t.Type == TransactionType.Buy)
            {
                quantity += t.Quantity;
                totalCost += t.Quantity * t.UnitPrice;
            }
            else
            {
                if (t.Quantity > quantity)
                    throw new DomainException(
                        $"Venda de {t.Quantity:0.######} excede a posição disponível ({quantity:0.######}) em {t.ExecutedAt:yyyy-MM-dd HH:mm:ss} UTC.");

                // RN4: vender não muda o preço médio, só reduz o custo proporcionalmente.
                // Multiplica antes de dividir para perder menos precisão.
                totalCost -= totalCost * t.Quantity / quantity;
                quantity -= t.Quantity;

                if (quantity == 0) totalCost = 0; // zerou a posição: elimina qualquer resíduo
            }
        }

        return new Position(assetId, quantity, totalCost);
    }
}
