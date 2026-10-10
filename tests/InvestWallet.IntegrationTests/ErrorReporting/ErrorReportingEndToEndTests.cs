using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InvestWallet.Application.ErrorReporting;
using InvestWallet.Application.Wallets;
using InvestWallet.Infrastructure.ErrorReporting;
using InvestWallet.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InvestWallet.IntegrationTests.ErrorReporting;

/// <summary>Do 500 real na API até a chamada HTTP ao "GitHub" (simulado): prova que as peças funcionam juntas.</summary>
[Collection(ApiCollection.Name)]
public class ErrorReportingEndToEndTests
{
    private const string FakeToken = "ghp_token_falso_para_teste_123";

    private readonly ApiFactory _factory;

    public ErrorReportingEndToEndTests(ApiFactory factory) => _factory = factory;

    private (HttpClient Client, FakeGitHubHandler Github) CreateClient(bool brokenRepository)
    {
        var github = new FakeGitHubHandler();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("IssueTracking:Provider", "GitHub");
            builder.UseSetting("IssueTracking:GitHub:Owner", "ana");
            builder.UseSetting("IssueTracking:GitHub:Repo", "invest-wallet");
            builder.UseSetting("IssueTracking:GitHub:Token", FakeToken);
            builder.ConfigureTestServices(services =>
            {
                // Troca só o transporte HTTP do cliente do GitHub: a configuração real (cabeçalhos, URL) continua valendo
                services.AddHttpClient(nameof(IIssueTracker)).ConfigurePrimaryHttpMessageHandler(() => github);

                if (brokenRepository)
                {
                    services.RemoveAll<IWalletRepository>();
                    services.AddScoped<IWalletRepository, BrokenWalletRepository>();
                }
            });
        }).CreateClient();
        return (client, github);
    }

    private static async Task WaitForCalls(FakeGitHubHandler github, int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (github.Calls.Count < count)
        {
            if (DateTime.UtcNow > deadline) Assert.Fail($"Esperava {count} chamada(s) ao GitHub; houve {github.Calls.Count}.");
            await Task.Delay(20);
        }
    }

    [Fact]
    public async Task Erro_500_vira_uma_issue_com_o_traceId_da_resposta_e_sem_dados_sensiveis()
    {
        var (client, github) = CreateClient(brokenRepository: true);

        var response = await client.GetAsync("/api/wallets");
        var traceId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("traceId").GetString()!;
        await WaitForCalls(github, 2); // busca + criação

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var create = github.Calls[1];
        Assert.Equal(HttpMethod.Post, create.Method);
        Assert.Equal("/repos/ana/invest-wallet/issues", create.PathAndQuery);

        using var issue = JsonDocument.Parse(create.Body);
        var body = issue.RootElement.GetProperty("body").GetString()!;
        var title = issue.RootElement.GetProperty("title").GetString()!;
        Assert.Contains(traceId, body);                                 // do erro no GitHub ao log, pelo mesmo id
        Assert.Contains("api/wallets", title);                          // padrão da rota
        Assert.Contains("InvalidOperationException", title);
        Assert.DoesNotContain(BrokenWalletRepository.Secret, body);      // segredo na mensagem da exceção
        Assert.Contains("Password=[REDACTED]", body);
        Assert.DoesNotContain("/home/", body);                           // usuário do sistema nos caminhos
        Assert.DoesNotContain(FakeToken, create.Body);                   // o token nunca vai no conteúdo
    }

    [Fact]
    public async Task A_chamada_ao_GitHub_usa_token_agente_e_versao_da_API_configurados()
    {
        var (client, github) = CreateClient(brokenRepository: true);

        await client.GetAsync("/api/wallets");
        await WaitForCalls(github, 2);

        foreach (var call in github.Calls)
        {
            Assert.Equal($"Bearer {FakeToken}", call.Headers["Authorization"]);
            Assert.Contains("InvestWallet", call.Headers["User-Agent"]);   // o GitHub recusa requisições sem User-Agent
            Assert.Equal("2022-11-28", call.Headers["X-GitHub-Api-Version"]);
            Assert.Contains("application/vnd.github+json", call.Headers["Accept"]);
        }
    }

    [Fact]
    public async Task O_mesmo_erro_repetido_vira_uma_so_issue_e_nao_uma_por_ocorrencia()
    {
        var (client, github) = CreateClient(brokenRepository: true);

        for (var i = 0; i < 5; i++) await client.GetAsync("/api/wallets");
        await WaitForCalls(github, 2);
        await Task.Delay(300); // dá tempo para uma duplicata aparecer, se existisse

        Assert.Equal(1, github.Calls.Count(c => c.Method == HttpMethod.Post)); // 1 criação para 5 erros
        Assert.Equal(1, github.Calls.Count(c => c.Method == HttpMethod.Get));
    }

    [Fact]
    public async Task Erros_esperados_do_cliente_nao_geram_issue()
    {
        var (client, github) = CreateClient(brokenRepository: false);

        var notFound = await client.GetAsync($"/api/wallets/{Guid.NewGuid()}");
        var invalid = await client.GetAsync("/api/wallets?page=abc");
        await Task.Delay(300);

        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Empty(github.Calls);
    }

    [Fact]
    public async Task Se_o_GitHub_esta_fora_do_ar_a_API_continua_respondendo_normalmente()
    {
        var (client, github) = CreateClient(brokenRepository: true);
        github.Responder = _ => throw new HttpRequestException("sem rede");

        var first = await client.GetAsync("/api/wallets");
        var health = await client.GetAsync("/health");
        var second = await client.GetAsync("/api/wallets");

        Assert.Equal(HttpStatusCode.InternalServerError, first.StatusCode); // o erro original continua sendo um 500 normal
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, second.StatusCode);
    }

    [Fact]
    public void Por_padrao_o_aviso_de_erros_esta_desligado_e_nada_e_registrado()
    {
        var reporter = _factory.Services.GetRequiredService<IErrorReporter>();

        Assert.IsType<NullErrorReporter>(reporter);
        Assert.Null(_factory.Services.GetService<ChannelErrorReporter>()); // nem fila nem worker foram criados
    }

    private static IServiceCollection Register(Dictionary<string, string?> settings) =>
        new ServiceCollection().AddErrorReporting(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

    [Fact]
    public void Ligado_com_configuracao_incompleta_a_aplicacao_recusa_iniciar_dizendo_o_que_falta()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Register(new() { ["IssueTracking:Provider"] = "GitHub", ["IssueTracking:GitHub:Owner"] = "ana" }));

        Assert.Contains("IssueTracking__GitHub__Repo", ex.Message);
        Assert.Contains("IssueTracking__GitHub__Token", ex.Message);
        Assert.DoesNotContain("IssueTracking__GitHub__Owner", ex.Message); // o que já foi informado não é cobrado de novo
    }

    [Fact]
    public void Provedor_desconhecido_e_recusado_em_vez_de_ignorado()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Register(new() { ["IssueTracking:Provider"] = "Jiraa" }));

        Assert.Contains("Jiraa", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("None")]
    [InlineData("none")]
    public void Sem_provedor_ou_None_fica_desligado(string? provider)
    {
        var services = Register(new() { ["IssueTracking:Provider"] = provider });

        Assert.Contains(services, d => d.ServiceType == typeof(IErrorReporter) && d.ImplementationType == typeof(NullErrorReporter));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IIssueTracker));
    }
}
