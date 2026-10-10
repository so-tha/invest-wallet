using System.Net;
using System.Net.Http.Json;
using InvestWallet.Application.Common;
using InvestWallet.Application.Wallets;

namespace InvestWallet.IntegrationTests;

[Collection(ApiCollection.Name)]
public class WalletsEndpointsTests : ApiTestBase
{
    public WalletsEndpointsTests(ApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Post_cria_e_devolve_201_com_Location()
    {
        var response = await Client.PostAsJsonAsync("/api/wallets", new { name = "  Minha carteira  ", description = "d" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync<WalletResponse>(response);
        Assert.Equal("Minha carteira", created.Name);
        Assert.EndsWith($"/api/wallets/{created.Id}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Get_por_id_devolve_o_que_foi_criado()
    {
        var created = await CreateWalletAsync("Para ler");

        var fetched = await ReadAsync<WalletResponse>(await Client.GetAsync($"/api/wallets/{created.Id}"));

        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("Para ler", fetched.Name);
    }

    [Fact]
    public async Task Put_atualiza_e_o_Get_seguinte_ve_a_mudanca()
    {
        var created = await CreateWalletAsync();

        var put = await Client.PutAsJsonAsync($"/api/wallets/{created.Id}", new { name = "Novo nome", description = (string?)null });
        var fetched = await ReadAsync<WalletResponse>(await Client.GetAsync($"/api/wallets/{created.Id}"));

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("Novo nome", fetched.Name);
    }

    [Fact]
    public async Task Delete_remove_e_depois_o_Get_devolve_404()
    {
        var created = await CreateWalletAsync();

        var delete = await Client.DeleteAsync($"/api/wallets/{created.Id}");
        var get = await Client.GetAsync($"/api/wallets/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Get_de_id_inexistente_devolve_404_em_problem_json_com_traceId()
    {
        var response = await Client.GetAsync($"/api/wallets/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await ReadProblemAsync(response);
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Post_com_nome_vazio_devolve_400_com_erros_por_campo()
    {
        var response = await Client.PostAsJsonAsync("/api/wallets", new { name = "  " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("O nome da carteira é obrigatório.", problem.GetProperty("errors").GetProperty("name")[0].GetString());
    }

    [Fact]
    public async Task Post_sem_campo_obrigatorio_e_tratado_pelo_nosso_validador_em_portugues()
    {
        var response = await Client.PostAsJsonAsync("/api/wallets", new { description = "sem nome" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("O nome da carteira é obrigatório.", problem.GetProperty("errors").GetProperty("name")[0].GetString());
    }

    [Fact]
    public async Task Post_com_json_malformado_devolve_400_sem_vazar_detalhes_internos()
    {
        var response = await Client.PostAsync("/api/wallets",
            new StringContent("{\"name\":", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("LineNumber", body);
        Assert.DoesNotContain("System.Text.Json", body);
    }

    [Fact]
    public async Task Lista_e_paginada_e_pageSize_e_limitado_a_100()
    {
        for (var i = 0; i < 3; i++) await CreateWalletAsync();

        var page = await ReadAsync<PagedResult<WalletResponse>>(await Client.GetAsync("/api/wallets?page=1&pageSize=2"));
        var capped = await ReadAsync<PagedResult<WalletResponse>>(await Client.GetAsync("/api/wallets?pageSize=100000"));

        Assert.Equal(2, page.Items.Count);
        Assert.True(page.TotalCount >= 3);
        Assert.Equal(100, capped.PageSize);
    }

    [Fact]
    public async Task Query_string_invalida_devolve_400_em_portugues()
    {
        var response = await Client.GetAsync("/api/wallets?page=abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Contains("não é válido", problem.GetProperty("errors").GetProperty("page")[0].GetString());
    }

    [Fact]
    public async Task Health_check_responde_200()
    {
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/health")).StatusCode);
    }
}
