using InvestWallet.Application.Exceptions;
using InvestWallet.Application.Transactions;
using InvestWallet.Application.Wallets;
using InvestWallet.Domain.Positions;

namespace InvestWallet.Application.Positions;

public class PositionService : IPositionService
{
    private const int Decimals = 6; // mesma precisão do banco

    private readonly ITransactionRepository _transactions;
    private readonly IWalletRepository _wallets;

    public PositionService(ITransactionRepository transactions, IWalletRepository wallets)
    {
        _transactions = transactions;
        _wallets = wallets;
    }

    public async Task<IReadOnlyList<PositionResponse>> GetByWalletAsync(Guid walletId, CancellationToken ct)
    {
        if (await _wallets.GetByIdAsync(walletId, ct) is null)
            throw new NotFoundException($"Carteira {walletId} não encontrada.");

        var all = await _transactions.GetAllByWalletAsync(walletId, ct);

        return all
            .GroupBy(t => t.AssetId)
            .Select(g => (Asset: g.First().Asset!, Position: PositionCalculator.Calculate(g.Key, g)))
            .Where(x => x.Position.Quantity > 0) // posição zerada não aparece
            .OrderBy(x => x.Asset.Ticker)
            .Select(x => new PositionResponse(
                x.Asset.Id, x.Asset.Ticker, x.Asset.Name, x.Asset.Type,
                x.Position.Quantity,
                Math.Round(x.Position.AveragePrice, Decimals, MidpointRounding.AwayFromZero),
                Math.Round(x.Position.TotalCost, Decimals, MidpointRounding.AwayFromZero)))
            .ToList();
    }
}
