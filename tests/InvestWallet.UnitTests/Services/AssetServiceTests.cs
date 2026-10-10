using InvestWallet.Application.Assets;
using InvestWallet.Application.Exceptions;
using InvestWallet.Domain.Entities;
using InvestWallet.Domain.Enums;
using NSubstitute;

namespace InvestWallet.UnitTests.Services;

public class AssetServiceTests
{
    private readonly IAssetRepository _repo = Substitute.For<IAssetRepository>();
    private readonly AssetService _service;

    public AssetServiceTests() => _service = new AssetService(_repo, new AssetRequestValidator());

    [Fact]
    public async Task Create_normaliza_o_ticker_antes_de_checar_duplicidade()
    {
        await _service.CreateAsync(new AssetRequest(" petr4 ", "Petrobras", AssetType.Stock), default);

        // A checagem usa o ticker JÁ normalizado, senão "petr4" e "PETR4" passariam como diferentes.
        await _repo.Received(1).TickerExistsAsync("PETR4", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_com_ticker_existente_lanca_Conflict_e_nao_persiste()
    {
        _repo.TickerExistsAsync("PETR4", null, Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.CreateAsync(new AssetRequest("petr4", "Outra", AssetType.Stock), default));

        await _repo.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Update_ignora_o_proprio_registro_ao_checar_duplicidade()
    {
        var asset = new Asset("HGLG11", "FII", AssetType.RealEstateFund);
        _repo.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        await _service.UpdateAsync(asset.Id, new AssetRequest("hglg11", "FII novo nome", AssetType.RealEstateFund), default);

        // excludeId = o próprio id: manter o mesmo ticker não pode conflitar consigo mesmo
        await _repo.Received(1).TickerExistsAsync("HGLG11", asset.Id, Arg.Any<CancellationToken>());
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_para_ticker_de_outro_ativo_lanca_Conflict_e_nao_salva()
    {
        var asset = new Asset("AAA1", "A", AssetType.Stock);
        _repo.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);
        _repo.TickerExistsAsync("BBB1", asset.Id, Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.UpdateAsync(asset.Id, new AssetRequest("BBB1", "A", AssetType.Stock), default));

        await _repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_de_ativo_com_transacoes_lanca_Conflict()
    {
        var asset = new Asset("AAA1", "A", AssetType.Stock);
        _repo.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);
        _repo.HasTransactionsAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<ConflictException>(() => _service.DeleteAsync(asset.Id, default));

        await _repo.DidNotReceiveWithAnyArgs().RemoveAsync(default!, default);
    }
}
