using FluentValidation;
using InvestWallet.Application.Exceptions;
using InvestWallet.Application.Wallets;
using InvestWallet.Domain.Entities;
using NSubstitute;

namespace InvestWallet.UnitTests.Services;

public class WalletServiceTests
{
    private readonly IWalletRepository _repo = Substitute.For<IWalletRepository>(); // MOCK
    private readonly WalletService _service;

    public WalletServiceTests() => _service = new WalletService(_repo, new WalletRequestValidator());

    [Fact]
    public async Task Get_de_id_inexistente_lanca_NotFound()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Wallet?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Create_valido_persiste_e_devolve_os_dados()
    {
        var response = await _service.CreateAsync(new WalletRequest("  Minha  ", "d"), default);

        Assert.Equal("Minha", response.Name);
        await _repo.Received(1).AddAsync(Arg.Is<Wallet>(w => w.Name == "Minha"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_invalido_lanca_ValidationException_e_nao_toca_no_repositorio()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(new WalletRequest("", null), default));

        await _repo.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Update_salva_as_alteracoes_na_entidade_carregada()
    {
        var wallet = new Wallet("Antigo", null);
        _repo.GetByIdAsync(wallet.Id, Arg.Any<CancellationToken>()).Returns(wallet);

        var response = await _service.UpdateAsync(wallet.Id, new WalletRequest("Novo", "x"), default);

        Assert.Equal("Novo", response.Name);
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_de_carteira_com_transacoes_lanca_Conflict_e_nao_remove()
    {
        var wallet = new Wallet("W", null);
        _repo.GetByIdAsync(wallet.Id, Arg.Any<CancellationToken>()).Returns(wallet);
        _repo.HasTransactionsAsync(wallet.Id, Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<ConflictException>(() => _service.DeleteAsync(wallet.Id, default));

        await _repo.DidNotReceiveWithAnyArgs().RemoveAsync(default!, default);
    }

    [Fact]
    public async Task Delete_sem_transacoes_remove()
    {
        var wallet = new Wallet("W", null);
        _repo.GetByIdAsync(wallet.Id, Arg.Any<CancellationToken>()).Returns(wallet);

        await _service.DeleteAsync(wallet.Id, default);

        await _repo.Received(1).RemoveAsync(wallet, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0, 0, 1, 20)]       // valores inválidos viram os padrões
    [InlineData(-5, 1000, 1, 100)]  // pageSize é limitado a 100
    [InlineData(3, 10, 3, 10)]      // valores válidos passam intactos
    public async Task List_normaliza_pagina_e_tamanho_e_calcula_o_skip(int page, int size, int expectedPage, int expectedSize)
    {
        _repo.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
             .Returns(((IReadOnlyList<Wallet>)new List<Wallet>(), 0));

        var result = await _service.ListAsync(page, size, default);

        Assert.Equal(expectedPage, result.Page);
        Assert.Equal(expectedSize, result.PageSize);
        await _repo.Received(1).ListAsync((expectedPage - 1) * expectedSize, expectedSize, Arg.Any<CancellationToken>());
    }
}
