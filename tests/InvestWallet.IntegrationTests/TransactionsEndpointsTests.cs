using System.Net;
using System.Net.Http.Json;
using InvestWallet.Application.Common;
using InvestWallet.Application.Positions;
using InvestWallet.Application.Transactions;
using InvestWallet.Domain.Enums;

namespace InvestWallet.IntegrationTests;

[Collection(ApiCollection.Name)]
public class TransactionsEndpointsTests : ApiTestBase
{
    public TransactionsEndpointsTests(ApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Fluxo_completo_compra_compra_venda_e_posicao()
    {
        var wallet = await CreateWalletAsync();
        var asset = await CreateAssetAsync();

        var buy1 = await PostTransactionAsync(wallet.Id, asset.Id, "Buy", 10, 10, "2026-01-01T12:00:00Z");
        var buy2 = await PostTransactionAsync(wallet.Id, asset.Id, "Buy", 10, 20, "2026-01-10T12:00:00Z");
        var sell = await PostTransactionAsync(wallet.Id, asset.Id, "Sell", 5, 99, "2026-01-20T12:00:00Z");

        Assert.Equal(HttpStatusCode.Created, buy1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, buy2.StatusCode);
        Assert.Equal(HttpStatusCode.Created, sell.StatusCode);

        var positions = await ReadAsync<List<PositionResponse>>(await Client.GetAsync($"/api/wallets/{wallet.Id}/positions"));
        var position = Assert.Single(positions);
        Assert.Equal(asset.Ticker, position.Ticker);
        Assert.Equal(15, position.Quantity);
        Assert.Equal(15, position.AveragePrice);   // venda não altera o preço médio
        Assert.Equal(225, position.TotalCost);
    }

    [Fact]
    public async Task Post_devolve_201_com_Location_e_Get_por_id_funciona()
    {
        var wallet = await CreateWalletAsync();
        var asset = await CreateAssetAsync();

        var response = await PostTransactionAsync(wallet.Id, asset.Id, "Buy", 3, 7.5m);
        var created = await ReadAsync<TransactionResponse>(response);
        var fetched = await ReadAsync<TransactionResponse>(await Client.GetAsync(response.Headers.Location));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(22.5m, fetched.TotalValue);
        Assert.Equal(TransactionType.Buy, fetched.Type);
    }

    [Fact]
    public async Task Venda_maior_que_a_posicao_devolve_422_e_nao_grava()
    {
        var wallet = await CreateWalletAsync();
        var asset = await CreateAssetAsync();
        await PostTransactionAsync(wallet.Id, asset.Id, "Buy", 10, 10);

        var response = await PostTransactionAsync(wallet.Id, asset.Id, "Sell", 11, 10, "2026-02-01T12:00:00Z");

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        var list = await ReadAsync<PagedResult<TransactionResponse>>(await Client.GetAsync($"/api/wallets/{wallet.Id}/transactions"));
        Assert.Equal(1, list.TotalCount);
    }

    [Fact]
    public async Task Venda_antes_da_compra_devolve_422_mesmo_que_o_total_feche()
    {
        var wallet = await CreateWalletAsync();
        var asset = await CreateAssetAsync();
        await PostTransactionAsync(wallet.Id, asset.Id, "Buy", 10, 10, "2026-01-10T12:00:00Z");

        var response = await PostTransactionAsync(wallet.Id, asset.Id, "Sell", 5, 10, "2026-01-05T12:00:00Z");

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }

    [Fact]
    public async Task Apagar_compra_que_sustenta_venda_posterior_devolve_422_RN6()
    {
        var wallet = await CreateWalletAsync();
        var asset = await CreateAssetAsync();
        var buy = await ReadAsync<TransactionResponse>(await PostTransactionAsync(wallet.Id, asset.Id, "Buy", 10, 10, "2026-01-01T12:00:00Z"));
        await PostTransactionAsync(wallet.Id, asset.Id, "Sell", 4, 10, "2026-01-02T12:00:00Z");

        var delete = await Client.DeleteAsync($"/api/wallets/{wallet.Id}/transactions/{buy.Id}");

        Assert.Equal((HttpStatusCode)422, delete.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/wallets/{wallet.Id}/transactions/{buy.Id}")).StatusCode);
    }

    [Fact]
    public async Task Apagar_a_venda_e_permitido_e_a_posicao_volta()
    {
        var wallet = await CreateWalletAsync();
        var asset = await CreateAssetAsync();
        await PostTransactionAsync(wallet.Id, asset.Id, "Buy", 10, 10, "2026-01-01T12:00:00Z");
        var sell = await ReadAsync<TransactionResponse>(await PostTransactionAsync(wallet.Id, asset.Id, "Sell", 4, 10, "2026-01-02T12:00:00Z"));

        var delete = await Client.DeleteAsync($"/api/wallets/{wallet.Id}/transactions/{sell.Id}");
        var positions = await ReadAsync<List<PositionResponse>>(await Client.GetAsync($"/api/wallets/{wallet.Id}/positions"));

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(10, Assert.Single(positions).Quantity);
    }

    [Fact]
    public async Task Posicao_totalmente_vendida_nao_aparece()
    {
        var wallet = await CreateWalletAsync();
        var asset = await CreateAssetAsync();
        await PostTransactionAsync(wallet.Id, asset.Id, "Buy", 5, 10, "2026-01-01T12:00:00Z");
        await PostTransactionAsync(wallet.Id, asset.Id, "Sell", 5, 12, "2026-01-02T12:00:00Z");

        var positions = await ReadAsync<List<PositionResponse>>(await Client.GetAsync($"/api/wallets/{wallet.Id}/positions"));

        Assert.Empty(positions);
    }

    [Fact]
    public async Task Carteira_com_transacoes_nao_pode_ser_apagada_nem_o_ativo_409_RN5()
    {
        var wallet = await CreateWalletAsync();
        var asset = await CreateAssetAsync();
        await PostTransactionAsync(wallet.Id, asset.Id, "Buy", 1, 1);

        Assert.Equal(HttpStatusCode.Conflict, (await Client.DeleteAsync($"/api/wallets/{wallet.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.DeleteAsync($"/api/assets/{asset.Id}")).StatusCode);
    }

    [Fact]
    public async Task Ativo_de_outra_carteira_nao_interfere_na_posicao()
    {
        var walletA = await CreateWalletAsync();
        var walletB = await CreateWalletAsync();
        var asset = await CreateAssetAsync();
        await PostTransactionAsync(walletA.Id, asset.Id, "Buy", 10, 10);

        // Vender na carteira B, que não tem nada: deve falhar mesmo que a A tenha saldo
        var response = await PostTransactionAsync(walletB.Id, asset.Id, "Sell", 1, 10, "2026-02-01T12:00:00Z");

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }

    [Fact]
    public async Task Transacao_de_uma_carteira_nao_e_acessivel_pela_rota_de_outra()
    {
        var walletA = await CreateWalletAsync();
        var walletB = await CreateWalletAsync();
        var asset = await CreateAssetAsync();
        var tx = await ReadAsync<TransactionResponse>(await PostTransactionAsync(walletA.Id, asset.Id, "Buy", 1, 1));

        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/wallets/{walletB.Id}/transactions/{tx.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.DeleteAsync($"/api/wallets/{walletB.Id}/transactions/{tx.Id}")).StatusCode);
    }

    [Fact]
    public async Task Carteira_inexistente_devolve_404_e_ativo_inexistente_no_corpo_devolve_422()
    {
        var wallet = await CreateWalletAsync();

        var noWallet = await PostTransactionAsync(Guid.NewGuid(), Guid.NewGuid(), "Buy", 1, 1);
        var noAsset = await PostTransactionAsync(wallet.Id, Guid.NewGuid(), "Buy", 1, 1);

        Assert.Equal(HttpStatusCode.NotFound, noWallet.StatusCode);
        Assert.Equal((HttpStatusCode)422, noAsset.StatusCode);
    }

    [Theory]
    [InlineData(0, 10, "quantity")]
    [InlineData(1.1234567, 10, "quantity")]
    [InlineData(1, -5, "unitPrice")]
    public async Task Valores_invalidos_devolvem_400_apontando_o_campo(double quantity, double price, string field)
    {
        var wallet = await CreateWalletAsync();
        var asset = await CreateAssetAsync();

        var response = await PostTransactionAsync(wallet.Id, asset.Id, "Buy", (decimal)quantity, (decimal)price);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ReadProblemAsync(response)).GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Data_no_futuro_devolve_400()
    {
        var wallet = await CreateWalletAsync();
        var asset = await CreateAssetAsync();

        var response = await PostTransactionAsync(wallet.Id, asset.Id, "Buy", 1, 1, "2999-01-01T00:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Corpo_vazio_devolve_400_listando_todos_os_campos_obrigatorios()
    {
        var wallet = await CreateWalletAsync();

        var response = await Client.PostAsJsonAsync($"/api/wallets/{wallet.Id}/transactions", new { });

        var errors = (await ReadProblemAsync(response)).GetProperty("errors");
        foreach (var field in new[] { "assetId", "quantity", "unitPrice", "executedAt" })
            Assert.True(errors.TryGetProperty(field, out _), $"faltou o erro do campo {field}");
    }

    [Fact]
    public async Task Filtros_por_ativo_e_periodo_e_ordenacao_mais_recente_primeiro()
    {
        var wallet = await CreateWalletAsync();
        var a = await CreateAssetAsync();
        var b = await CreateAssetAsync();
        await PostTransactionAsync(wallet.Id, a.Id, "Buy", 1, 1, "2026-01-01T12:00:00Z");
        await PostTransactionAsync(wallet.Id, a.Id, "Buy", 1, 1, "2026-03-01T12:00:00Z");
        await PostTransactionAsync(wallet.Id, b.Id, "Buy", 1, 1, "2026-03-01T12:00:00Z");

        var byAsset = await ReadAsync<PagedResult<TransactionResponse>>(
            await Client.GetAsync($"/api/wallets/{wallet.Id}/transactions?assetId={a.Id}"));
        var byPeriod = await ReadAsync<PagedResult<TransactionResponse>>(
            await Client.GetAsync($"/api/wallets/{wallet.Id}/transactions?from=2026-02-01T00:00:00Z"));

        Assert.Equal(2, byAsset.TotalCount);
        Assert.True(byAsset.Items[0].ExecutedAt > byAsset.Items[1].ExecutedAt);
        Assert.Equal(2, byPeriod.TotalCount);
    }

    [Fact]
    public async Task Fuso_horario_e_convertido_para_UTC()
    {
        var wallet = await CreateWalletAsync();
        var asset = await CreateAssetAsync();

        var response = await PostTransactionAsync(wallet.Id, asset.Id, "Buy", 1, 1, "2026-01-01T09:00:00-03:00");
        var created = await ReadAsync<TransactionResponse>(response);

        Assert.Equal(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc), created.ExecutedAt);
    }
}
