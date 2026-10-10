using System.Net;
using System.Net.Http.Json;

namespace InvestWallet.IntegrationTests;

/// <summary>
/// Provam as garantias que testes unitários não conseguem dar: com requisições realmente simultâneas e um
/// PostgreSQL real, a trava (advisory lock) e o índice único mantêm o sistema consistente.
/// </summary>
[Collection(ApiCollection.Name)]
public class ConcurrencyTests : ApiTestBase
{
    public ConcurrencyTests(ApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Vendas_simultaneas_nunca_vendem_mais_do_que_a_posicao()
    {
        // Saldo 20; 8 vendas de 10 ao mesmo tempo: só 2 podem passar.
        // Sem a trava, todas leriam saldo 20, passariam na validação e o saldo ficaria negativo.
        var wallet = await CreateWalletAsync();
        var asset = await CreateAssetAsync();
        await PostTransactionAsync(wallet.Id, asset.Id, "Buy", 20, 10, "2026-01-01T12:00:00Z");

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => PostTransactionAsync(wallet.Id, asset.Id, "Sell", 10, 10, "2026-01-02T12:00:00Z"))));

        Assert.Equal(2, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(6, responses.Count(r => (int)r.StatusCode == 422));
        var positions = await Client.GetFromJsonAsync<List<System.Text.Json.JsonElement>>($"/api/wallets/{wallet.Id}/positions", Json);
        Assert.Empty(positions!); // zerou, não ficou negativo
    }

    [Fact]
    public async Task Criacoes_simultaneas_do_mesmo_ticker_resultam_em_um_unico_registro()
    {
        // A checagem "já existe?" tem janela de corrida; o índice único do banco é quem garante.
        var ticker = UniqueTicker();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            Client.PostAsJsonAsync("/api/assets", new { ticker, name = "Corrida", type = "Etf" }))));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.DoesNotContain(responses, r => (int)r.StatusCode >= 500); // nenhum 500 vazando
    }

    [Fact]
    public async Task Compras_e_vendas_em_carteiras_diferentes_nao_se_bloqueiam_nem_se_misturam()
    {
        var asset = await CreateAssetAsync();
        var wallets = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => CreateWalletAsync()));

        // Cada carteira compra 10 e tenta vender 10 + 10 em paralelo: em cada uma só 1 venda passa.
        var results = await Task.WhenAll(wallets.Select(async w =>
        {
            await PostTransactionAsync(w.Id, asset.Id, "Buy", 10, 10, "2026-01-01T12:00:00Z");
            var sells = await Task.WhenAll(
                PostTransactionAsync(w.Id, asset.Id, "Sell", 10, 10, "2026-01-02T12:00:00Z"),
                PostTransactionAsync(w.Id, asset.Id, "Sell", 10, 10, "2026-01-02T12:00:00Z"));
            return sells.Count(s => s.StatusCode == HttpStatusCode.Created);
        }));

        Assert.All(results, created => Assert.Equal(1, created));
    }
}
