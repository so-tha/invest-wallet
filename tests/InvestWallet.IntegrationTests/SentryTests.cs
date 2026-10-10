using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using InvestWallet.Application.Wallets;
using InvestWallet.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sentry;
using Sentry.AspNetCore;
using Sentry.Extensibility;
using Sentry.Protocol.Envelopes;

namespace InvestWallet.IntegrationTests;

/// <summary>
/// Verifica a integração com o Sentry SEM rede: o transporte é trocado por um que descarta tudo, e um BeforeSend
/// registra os eventos que SERIAM enviados. Provam o que sai, o que não sai e que sem DSN nada é ativado.
/// </summary>
[Collection(ApiCollection.Name)]
public class SentryTests
{
    // DSN falso, só para ativar o SDK. Com o transporte descartado, nada chega a este endereço.
    private const string FakeDsn = "https://public@o0.ingest.sentry.io/0";

    private readonly ApiFactory _factory;

    public SentryTests(ApiFactory factory) => _factory = factory;

    private sealed class DiscardingTransport : ITransport
    {
        public Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private (HttpClient Client, ConcurrentQueue<SentryEvent> Events) CreateClient(bool brokenRepository)
    {
        var events = new ConcurrentQueue<SentryEvent>();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Sentry:Dsn", FakeDsn);
            builder.ConfigureTestServices(services =>
            {
                services.Configure<SentryAspNetCoreOptions>(options =>
                {
                    options.Transport = new DiscardingTransport();
                    options.SetBeforeSend((e, _) => { events.Enqueue(e); return null; }); // registra e descarta
                });

                if (brokenRepository)
                {
                    services.RemoveAll<IWalletRepository>();
                    services.AddScoped<IWalletRepository, BrokenWalletRepository>();
                }
            });
        }).CreateClient();
        return (client, events);
    }

    [Fact]
    public void Sem_DSN_configurado_o_DSN_fica_vazio_que_e_como_o_SDK_se_desativa()
    {
        // Não checamos IHub.IsEnabled: o SentrySdk é estado estático global e um host de outro teste
        // (que usa DSN falso) o deixaria ativado, tornando o resultado dependente da ordem dos testes.
        var options = _factory.Services.GetRequiredService<IOptions<SentryAspNetCoreOptions>>().Value;

        Assert.Equal("", options.Dsn);
    }

    [Fact]
    public async Task Erro_500_gera_exatamente_um_evento_com_o_traceId_da_resposta_como_tag()
    {
        var (client, events) = CreateClient(brokenRepository: true);

        var response = await client.GetAsync("/api/wallets");
        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var captured = Assert.Single(events);
        Assert.IsType<InvalidOperationException>(captured.Exception);
        Assert.Equal(problem.GetProperty("traceId").GetString(), captured.Tags["trace_id"]); // liga o Sentry ao log
    }

    [Fact]
    public async Task O_nome_da_maquina_nao_vai_como_server_name_nem_o_usuario_do_sistema_nos_caminhos()
    {
        var (client, events) = CreateClient(brokenRepository: true);

        await client.GetAsync("/api/wallets");

        var captured = Assert.Single(events);
        Assert.Equal("investwallet-api", captured.ServerName);
        Assert.NotEqual(Environment.MachineName, captured.ServerName);
        var paths = captured.SentryExceptions!.SelectMany(e => e.Stacktrace?.Frames ?? [])
            .SelectMany(f => new[] { f.FileName, f.AbsolutePath }).Where(p => p is not null);
        Assert.DoesNotContain(paths, p => p!.StartsWith("/home/") || p.StartsWith("/Users/"));
    }

    [Fact]
    public async Task Segredos_na_mensagem_da_excecao_sao_removidos_antes_do_envio()
    {
        var (client, events) = CreateClient(brokenRepository: true);

        await client.GetAsync("/api/wallets");

        var captured = Assert.Single(events);
        var sentMessages = captured.SentryExceptions!.Select(e => e.Value).ToList();
        Assert.All(sentMessages, message =>
        {
            Assert.DoesNotContain(BrokenWalletRepository.Secret, message);
            Assert.Contains("Password=[REDACTED]", message);
        });
    }

    [Fact]
    public async Task Cabecalhos_sensiveis_cookies_corpo_e_IP_nao_vao_no_evento()
    {
        var (client, events) = CreateClient(brokenRepository: true);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/wallets");
        request.Headers.Add("Authorization", "Bearer token-do-usuario-123");
        request.Headers.Add("Cookie", "sessao=abc-segredo");

        await client.SendAsync(request);

        var captured = Assert.Single(events);
        Assert.Null(captured.User.IpAddress);
        Assert.Null(captured.Request.Data);       // sem corpo da requisição
        Assert.Empty(captured.Request.Cookies ?? "");
        var headers = string.Join(";", captured.Request.Headers.Select(h => $"{h.Key}={h.Value}"));
        Assert.DoesNotContain("token-do-usuario-123", headers);
        Assert.DoesNotContain("abc-segredo", headers);
    }

    [Theory]
    [InlineData("/api/wallets/00000000-0000-0000-0000-000000000001", 404)] // NotFoundException
    [InlineData("/api/wallets?page=abc", 400)]                              // validação do MVC
    public async Task Erros_esperados_do_cliente_nao_geram_evento(string url, int expectedStatus)
    {
        var (client, events) = CreateClient(brokenRepository: false);

        var response = await client.GetAsync(url);

        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Empty(events);
    }

    [Fact]
    public async Task Conflito_409_e_regra_de_negocio_422_tambem_nao_geram_evento()
    {
        var (client, events) = CreateClient(brokenRepository: false);
        var ticker = "S" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var asset = new { ticker, name = "x", type = "Stock" };
        await client.PostAsJsonAsync("/api/assets", asset);

        var conflict = await client.PostAsJsonAsync("/api/assets", asset);                 // 409
        var business = await client.PostAsJsonAsync($"/api/wallets/{Guid.NewGuid()}/transactions", // 404 (carteira inexistente)
            new { assetId = Guid.NewGuid(), type = "Buy", quantity = 1, unitPrice = 1, executedAt = "2026-01-01T00:00:00Z" });

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, business.StatusCode);
        Assert.Empty(events);
    }
}
