using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InvestWallet.IntegrationTests;

/// <summary>Liveness (/health) responde "estou de pé"; readiness (/health/ready) responde "posso atender", incluindo o banco.</summary>
[Collection(ApiCollection.Name)]
public class HealthCheckTests : ApiTestBase
{
    private readonly ApiFactory _factory;

    public HealthCheckTests(ApiFactory factory) : base(factory) => _factory = factory;

    [Fact]
    public async Task Readiness_consulta_o_banco_de_verdade_e_responde_200_quando_ele_esta_ok()
    {
        var response = await Client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Quando_um_check_de_prontidao_falha_readiness_devolve_503_mas_liveness_continua_200()
    {
        using var client = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.AddHealthChecks().AddCheck("dependencia-fora-do-ar",
                () => HealthCheckResult.Unhealthy("simulando falha"), tags: ["ready"])))
            .CreateClient();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
        // Se a liveness falhasse junto, o orquestrador reiniciaria o container sem necessidade (o problema é a dependência, não o processo)
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }
}
