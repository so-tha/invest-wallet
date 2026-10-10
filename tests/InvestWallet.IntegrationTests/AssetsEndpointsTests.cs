using System.Net;
using System.Net.Http.Json;
using InvestWallet.Application.Assets;
using InvestWallet.Domain.Enums;

namespace InvestWallet.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AssetsEndpointsTests : ApiTestBase
{
    public AssetsEndpointsTests(ApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Post_normaliza_o_ticker_e_serializa_o_enum_como_texto()
    {
        var ticker = UniqueTicker();

        var response = await Client.PostAsJsonAsync("/api/assets", new { ticker = $" {ticker.ToLowerInvariant()} ", name = "X", type = "Etf" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync<AssetResponse>(response);
        Assert.Equal(ticker, created.Ticker);
        Assert.Equal(AssetType.Etf, created.Type);
        Assert.Contains("\"Etf\"", await Client.GetStringAsync($"/api/assets/{created.Id}")); // texto, não número
    }

    [Fact]
    public async Task Ticker_duplicado_devolve_409_mesmo_com_caixa_diferente()
    {
        var asset = await CreateAssetAsync();

        var response = await Client.PostAsJsonAsync("/api/assets",
            new { ticker = asset.Ticker.ToLowerInvariant(), name = "Outro", type = "Stock" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Put_mantendo_o_proprio_ticker_e_permitido_mas_roubar_o_de_outro_da_409()
    {
        var a = await CreateAssetAsync();
        var b = await CreateAssetAsync();

        var same = await Client.PutAsJsonAsync($"/api/assets/{a.Id}", new { ticker = a.Ticker, name = "Renomeado", type = "Stock" });
        var steal = await Client.PutAsJsonAsync($"/api/assets/{a.Id}", new { ticker = b.Ticker, name = "x", type = "Stock" });

        Assert.Equal(HttpStatusCode.OK, same.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, steal.StatusCode);
    }

    [Theory]
    [InlineData("Banana")] // texto que não existe no enum
    [InlineData(99)]       // número fora do enum
    public async Task Tipo_invalido_devolve_400(object type)
    {
        var response = await Client.PostAsJsonAsync("/api/assets", new { ticker = UniqueTicker(), name = "x", type });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Todos_os_campos_invalidos_geram_erros_por_campo_de_uma_so_vez()
    {
        var response = await Client.PostAsJsonAsync("/api/assets", new { ticker = "", name = "", type = 99 });

        var errors = (await ReadProblemAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("ticker", out _));
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("type", out _));
    }

    [Fact]
    public async Task Get_de_ativo_inexistente_devolve_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/assets/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Delete_de_ativo_sem_transacoes_devolve_204_e_depois_404()
    {
        var asset = await CreateAssetAsync();

        var delete = await Client.DeleteAsync($"/api/assets/{asset.Id}");
        var get = await Client.GetAsync($"/api/assets/{asset.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Lista_vem_ordenada_por_ticker_e_paginada()
    {
        var prefix = "ZZ" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        foreach (var suffix in new[] { "C", "A", "B" }) await CreateAssetAsync(prefix + suffix);

        var page = await ReadAsync<InvestWallet.Application.Common.PagedResult<AssetResponse>>(
            await Client.GetAsync("/api/assets?page=1&pageSize=100"));

        var mine = page.Items.Where(a => a.Ticker.StartsWith(prefix)).Select(a => a.Ticker).ToList();
        Assert.Equal([prefix + "A", prefix + "B", prefix + "C"], mine);
    }
}
