using FluentValidation;
using InvestWallet.Application.Assets;
using InvestWallet.Application.Exceptions;
using InvestWallet.Application.Transactions;
using InvestWallet.Application.Wallets;
using InvestWallet.Domain.Entities;
using InvestWallet.Domain.Enums;
using InvestWallet.Domain.Exceptions;
using InvestWallet.UnitTests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace InvestWallet.UnitTests.Services;

public class TransactionServiceTests
{
    private readonly FakeTransactionRepository _transactions = new();
    private readonly IWalletRepository _wallets = Substitute.For<IWalletRepository>();
    private readonly IAssetRepository _assets = Substitute.For<IAssetRepository>();
    private readonly TransactionService _service;

    private readonly Wallet _wallet = new("Carteira", null);
    private readonly Asset _asset = TestData.NewAsset("PETR4");

    public TransactionServiceTests()
    {
        _wallets.GetByIdAsync(_wallet.Id, Arg.Any<CancellationToken>()).Returns(_wallet);
        _assets.GetByIdAsync(_asset.Id, Arg.Any<CancellationToken>()).Returns(_asset);

        _service = new TransactionService(
            _transactions, _wallets, _assets,
            new TransactionRequestValidator(TestData.Clock()),
            NullLogger<TransactionService>.Instance);
    }

    private TransactionRequest Request(TransactionType type, decimal qty, decimal price = 10, int daysAgo = 1) =>
        new(_asset.Id, type, qty, price, TestData.Now.AddDays(-daysAgo));

    [Fact]
    public async Task Compra_valida_e_gravada_e_devolve_o_ticker()
    {
        var response = await _service.CreateAsync(_wallet.Id, Request(TransactionType.Buy, 10), default);

        Assert.Equal("PETR4", response.AssetTicker);
        Assert.Equal(100, response.TotalValue);
        Assert.Single(_transactions.Stored);
    }

    [Fact]
    public async Task Venda_maior_que_a_posicao_lanca_DomainException_e_nao_grava()
    {
        _transactions.Seed(_asset, TestData.Tx(_wallet.Id, _asset, TransactionType.Buy, 10, 10, daysBeforeNow: 5));

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _service.CreateAsync(_wallet.Id, Request(TransactionType.Sell, 11), default));

        Assert.Contains("excede a posição disponível", ex.Message);
        Assert.Single(_transactions.Stored); // só a compra original
    }

    [Fact]
    public async Task Venda_exatamente_igual_a_posicao_e_aceita()
    {
        _transactions.Seed(_asset, TestData.Tx(_wallet.Id, _asset, TransactionType.Buy, 10, 10, 5));

        await _service.CreateAsync(_wallet.Id, Request(TransactionType.Sell, 10), default);

        Assert.Equal(2, _transactions.Stored.Count);
    }

    [Fact]
    public async Task Venda_com_data_anterior_a_compra_e_recusada_mesmo_havendo_saldo_hoje()
    {
        _transactions.Seed(_asset, TestData.Tx(_wallet.Id, _asset, TransactionType.Buy, 10, 10, daysBeforeNow: 2));

        // Venda "retroativa": 5 dias atrás, antes da compra (2 dias atrás)
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.CreateAsync(_wallet.Id, Request(TransactionType.Sell, 1, daysAgo: 5), default));
    }

    [Fact]
    public async Task Carteira_inexistente_lanca_NotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateAsync(Guid.NewGuid(), Request(TransactionType.Buy, 1), default));
    }

    [Fact]
    public async Task Ativo_inexistente_lanca_DomainException_422()
    {
        var request = Request(TransactionType.Buy, 1) with { AssetId = Guid.NewGuid() };

        await Assert.ThrowsAsync<DomainException>(() => _service.CreateAsync(_wallet.Id, request, default));
    }

    [Fact]
    public async Task Requisicao_invalida_falha_na_validacao_antes_de_consultar_o_banco()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(_wallet.Id, Request(TransactionType.Buy, 0), default));

        await _wallets.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }

    [Fact]
    public async Task Remover_compra_que_sustenta_uma_venda_posterior_e_recusado_RN6()
    {
        var buy = TestData.Tx(_wallet.Id, _asset, TransactionType.Buy, 10, 10, daysBeforeNow: 10);
        var sell = TestData.Tx(_wallet.Id, _asset, TransactionType.Sell, 4, 10, daysBeforeNow: 5);
        _transactions.Seed(_asset, buy, sell);

        await Assert.ThrowsAsync<DomainException>(() => _service.DeleteAsync(_wallet.Id, buy.Id, default));

        Assert.Equal(2, _transactions.Stored.Count); // nada foi removido
    }

    [Fact]
    public async Task Remover_a_venda_e_sempre_permitido()
    {
        var buy = TestData.Tx(_wallet.Id, _asset, TransactionType.Buy, 10, 10, 10);
        var sell = TestData.Tx(_wallet.Id, _asset, TransactionType.Sell, 4, 10, 5);
        _transactions.Seed(_asset, buy, sell);

        await _service.DeleteAsync(_wallet.Id, sell.Id, default);

        Assert.Single(_transactions.Stored);
    }

    [Fact]
    public async Task Remover_transacao_de_outra_carteira_lanca_NotFound()
    {
        var otherWallet = new Wallet("Outra", null);
        _wallets.GetByIdAsync(otherWallet.Id, Arg.Any<CancellationToken>()).Returns(otherWallet);
        var tx = TestData.Tx(_wallet.Id, _asset, TransactionType.Buy, 1, 1, 1);
        _transactions.Seed(_asset, tx);

        // A transação existe, mas pertence a outra carteira: não pode vazar nem ser apagada por esta rota.
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(otherWallet.Id, tx.Id, default));

        Assert.Single(_transactions.Stored);
    }

    [Fact]
    public async Task List_filtra_por_ativo_e_periodo()
    {
        var other = TestData.NewAsset("VALE3");
        _transactions.Seed(_asset,
            TestData.Tx(_wallet.Id, _asset, TransactionType.Buy, 1, 1, daysBeforeNow: 30),
            TestData.Tx(_wallet.Id, _asset, TransactionType.Buy, 1, 1, daysBeforeNow: 3));
        _transactions.Seed(other, TestData.Tx(_wallet.Id, other, TransactionType.Buy, 1, 1, daysBeforeNow: 3));

        var result = await _service.ListAsync(
            _wallet.Id, _asset.Id, TestData.Now.AddDays(-10), null, page: 1, pageSize: 20, default);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("PETR4", result.Items.Single().AssetTicker);
    }
}
