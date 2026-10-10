using System.Net;
using InvestWallet.Application.Wallets;
using InvestWallet.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InvestWallet.IntegrationTests;

/// <summary>
/// O caminho do erro 500: o que acontece quando algo realmente inesperado falha. A propriedade de segurança
/// aqui é NÃO devolver ao cliente a mensagem interna (que pode conter nomes de tabela, senhas, caminhos...).
/// </summary>
[Collection(ApiCollection.Name)]
public class UnexpectedErrorTests
{
    private readonly ApiFactory _factory;

    public UnexpectedErrorTests(ApiFactory factory) => _factory = factory;

    private HttpClient ClientWithBrokenRepository() => _factory
        .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IWalletRepository>();
            services.AddScoped<IWalletRepository, BrokenWalletRepository>();
        }))
        .CreateClient();

    [Fact]
    public async Task Falha_inesperada_devolve_500_generico_sem_vazar_a_mensagem_interna()
    {
        using var client = ClientWithBrokenRepository();

        var response = await client.GetAsync("/api/wallets");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.DoesNotContain(BrokenWalletRepository.Secret, body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("   at ", body); // sem stack trace
        Assert.Contains("traceId", body);       // mas com o id para o suporte achar o erro no log
    }

    [Fact]
    public async Task O_500_nao_derruba_a_aplicacao_e_as_proximas_requisicoes_continuam_funcionando()
    {
        using var client = ClientWithBrokenRepository();

        Assert.Equal(HttpStatusCode.InternalServerError, (await client.GetAsync("/api/wallets")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, (await client.GetAsync("/api/wallets")).StatusCode);
    }
}
