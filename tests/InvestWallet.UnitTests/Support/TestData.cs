using InvestWallet.Domain.Entities;
using InvestWallet.Domain.Enums;
using Microsoft.Extensions.Time.Testing;

namespace InvestWallet.UnitTests.Support;

public static class TestData
{
    /// <summary>"Agora" fixo dos testes: 2026-06-01 12:00 UTC.</summary>
    public static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    public static FakeTimeProvider Clock() => new(Now);

    public static Asset NewAsset(string ticker = "PETR4", AssetType type = AssetType.Stock) =>
        new(ticker, $"Ativo {ticker}", type);

    public static Transaction Tx(Guid walletId, Asset asset, TransactionType type, decimal qty, decimal price, int daysBeforeNow) =>
        new(walletId, asset.Id, type, qty, price, Now.UtcDateTime.AddDays(-daysBeforeNow));
}
