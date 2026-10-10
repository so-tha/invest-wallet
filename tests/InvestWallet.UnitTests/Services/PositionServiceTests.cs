using InvestWallet.Application.Exceptions;
using InvestWallet.Application.Positions;
using InvestWallet.Application.Wallets;
using InvestWallet.Domain.Entities;
using InvestWallet.Domain.Enums;
using InvestWallet.UnitTests.Support;
using NSubstitute;

namespace InvestWallet.UnitTests.Services;

public class PositionServiceTests
{
    private readonly FakeTransactionRepository _transactions = new();
    private readonly IWalletRepository _wallets = Substitute.For<IWalletRepository>();
    private readonly Wallet _wallet = new("W", null);
    private readonly PositionService _service;

    public PositionServiceTests()
    {
        _wallets.GetByIdAsync(_wallet.Id, Arg.Any<CancellationToken>()).Returns(_wallet);
        _service = new PositionService(_transactions, _wallets);
    }

    [Fact]
    public async Task Carteira_inexistente_lanca_NotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByWalletAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Carteira_sem_transacoes_devolve_lista_vazia()
    {
        Assert.Empty(await _service.GetByWalletAsync(_wallet.Id, default));
    }

    [Fact]
    public async Task Agrupa_por_ativo_ordena_por_ticker_e_omite_posicao_zerada()
    {
        var vale = TestData.NewAsset("VALE3");
        var petr = TestData.NewAsset("PETR4");
        var itub = TestData.NewAsset("ITUB4");
        _transactions.Seed(vale, TestData.Tx(_wallet.Id, vale, TransactionType.Buy, 5, 60, 10));
        _transactions.Seed(petr,
            TestData.Tx(_wallet.Id, petr, TransactionType.Buy, 10, 10, 10),
            TestData.Tx(_wallet.Id, petr, TransactionType.Buy, 10, 20, 9));
        _transactions.Seed(itub, // comprou e vendeu tudo: não deve aparecer
            TestData.Tx(_wallet.Id, itub, TransactionType.Buy, 3, 30, 10),
            TestData.Tx(_wallet.Id, itub, TransactionType.Sell, 3, 35, 9));

        var positions = await _service.GetByWalletAsync(_wallet.Id, default);

        Assert.Equal(["PETR4", "VALE3"], positions.Select(p => p.Ticker));
        var petr4 = positions[0];
        Assert.Equal(20, petr4.Quantity);
        Assert.Equal(15, petr4.AveragePrice);
        Assert.Equal(300, petr4.TotalCost);
    }

    [Fact]
    public async Task Preco_medio_e_arredondado_em_6_casas()
    {
        var asset = TestData.NewAsset("DIV1");
        // 1 × 1,00 + 2 × 1,50 = custo 4 para 3 unidades → preço médio 1,3333333... (dízima)
        _transactions.Seed(asset,
            TestData.Tx(_wallet.Id, asset, TransactionType.Buy, 1, 1.00m, 10),
            TestData.Tx(_wallet.Id, asset, TransactionType.Buy, 2, 1.50m, 9));

        var position = (await _service.GetByWalletAsync(_wallet.Id, default)).Single();

        Assert.Equal(1.333333m, position.AveragePrice);
        Assert.Equal(4m, position.TotalCost);
    }
}
