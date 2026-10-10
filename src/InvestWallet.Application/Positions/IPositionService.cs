namespace InvestWallet.Application.Positions;

public interface IPositionService
{
    /// <summary>Posições abertas (quantidade &gt; 0) da carteira, ordenadas por ticker.</summary>
    Task<IReadOnlyList<PositionResponse>> GetByWalletAsync(Guid walletId, CancellationToken ct);
}
