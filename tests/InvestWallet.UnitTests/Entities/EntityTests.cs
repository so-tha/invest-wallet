using InvestWallet.Domain.Entities;
using InvestWallet.Domain.Enums;
using InvestWallet.Domain.Exceptions;

namespace InvestWallet.UnitTests.Entities;

public class WalletTests
{
    [Fact]
    public void Cria_com_nome_sem_espacos_nas_pontas_e_id_gerado()
    {
        var wallet = new Wallet("  Aposentadoria  ", "  longo prazo ");

        Assert.Equal("Aposentadoria", wallet.Name);
        Assert.Equal("longo prazo", wallet.Description);
        Assert.NotEqual(Guid.Empty, wallet.Id);
        Assert.Equal(DateTimeKind.Utc, wallet.CreatedAt.Kind);
    }

    [Fact]
    public void Descricao_em_branco_vira_nula()
    {
        Assert.Null(new Wallet("X", "   ").Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Nome_vazio_e_recusado(string name)
    {
        Assert.Throws<DomainException>(() => new Wallet(name, null));
    }

    [Fact]
    public void Update_valida_e_altera_sem_mudar_o_id()
    {
        var wallet = new Wallet("A", null);
        var id = wallet.Id;

        wallet.Update("B", "desc");

        Assert.Equal("B", wallet.Name);
        Assert.Equal(id, wallet.Id);
        Assert.Throws<DomainException>(() => wallet.Update("", null));
    }
}

public class AssetTests
{
    [Fact]
    public void Ticker_e_normalizado_para_maiusculo_sem_espacos()
    {
        var asset = new Asset("  petr4 ", "Petrobras", AssetType.Stock);

        Assert.Equal("PETR4", asset.Ticker);
    }

    [Fact]
    public void Tipo_inexistente_e_recusado()
    {
        Assert.Throws<DomainException>(() => new Asset("X", "x", (AssetType)99));
    }

    [Theory]
    [InlineData("", "nome")]
    [InlineData("TICK", "")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU", "nome")] // 21 caracteres: acima do limite de 20
    public void Ticker_ou_nome_invalidos_sao_recusados(string ticker, string name)
    {
        Assert.Throws<DomainException>(() => new Asset(ticker, name, AssetType.Etf));
    }
}
