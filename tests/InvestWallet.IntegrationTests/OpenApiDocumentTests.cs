using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace InvestWallet.IntegrationTests;

/// <summary>
/// Testes do CONTRATO da API: garantem que o documento OpenAPI continua válido, completo e fiel ao que a API faz.
/// Sem eles, a documentação "apodrece" em silêncio quando alguém muda ou cria um endpoint.
/// </summary>
[Collection(ApiCollection.Name)]
public class OpenApiDocumentTests : ApiTestBase
{
    private const string SwaggerUrl = "/swagger/v1/swagger.json";

    private readonly ApiFactory _factory;

    public OpenApiDocumentTests(ApiFactory factory) : base(factory) => _factory = factory;

    private async Task<OpenApiDocument> LoadDocumentAsync()
    {
        var json = await Client.GetStringAsync(SwaggerUrl);
        var document = new OpenApiStringReader().Read(json, out var diagnostic);
        Assert.Empty(diagnostic.Errors); // o documento segue a especificação OpenAPI
        return document;
    }

    private static IEnumerable<(string Method, string Path, OpenApiOperation Operation)> Operations(OpenApiDocument doc) =>
        doc.Paths.SelectMany(p => p.Value.Operations.Select(o => (o.Key.ToString().ToUpperInvariant(), p.Key, o.Value)));

    [Fact]
    public async Task Documento_e_valido_e_expoe_exatamente_os_15_endpoints_planejados()
    {
        var document = await LoadDocumentAsync();

        var actual = Operations(document).Select(o => $"{o.Method} {o.Path}").Order().ToArray();

        string[] expected =
        [
            "DELETE /api/assets/{id}",
            "DELETE /api/wallets/{id}",
            "DELETE /api/wallets/{walletId}/transactions/{id}",
            "GET /api/assets",
            "GET /api/assets/{id}",
            "GET /api/wallets",
            "GET /api/wallets/{id}",
            "GET /api/wallets/{walletId}/positions",
            "GET /api/wallets/{walletId}/transactions",
            "GET /api/wallets/{walletId}/transactions/{id}",
            "POST /api/assets",
            "POST /api/wallets",
            "POST /api/wallets/{walletId}/transactions",
            "PUT /api/assets/{id}",
            "PUT /api/wallets/{id}",
        ];
        Assert.Equal(expected.Order(), actual);
    }

    [Fact]
    public async Task Toda_operacao_tem_resumo_tag_resposta_de_sucesso_e_de_erro_500()
    {
        var document = await LoadDocumentAsync();

        foreach (var (method, path, op) in Operations(document))
        {
            var name = $"{method} {path}";
            Assert.False(string.IsNullOrWhiteSpace(op.Summary), $"{name}: sem resumo");
            Assert.NotEmpty(op.Tags);
            Assert.Contains(op.Responses.Keys, code => code.StartsWith('2'));
            Assert.True(op.Responses.ContainsKey("500"), $"{name}: 500 não documentado");
        }
    }

    [Fact]
    public async Task Operacoes_com_corpo_ou_query_documentam_400_e_rotas_com_recurso_documentam_404()
    {
        var document = await LoadDocumentAsync();

        foreach (var (method, path, op) in Operations(document))
        {
            var name = $"{method} {path}";
            var temEntrada = op.RequestBody is not null || op.Parameters.Any(p => p.In == ParameterLocation.Query);
            if (temEntrada) Assert.True(op.Responses.ContainsKey("400"), $"{name}: 400 não documentado");

            var apontaParaRecurso = path.Contains("{id}") || path.Contains("{walletId}");
            if (apontaParaRecurso) Assert.True(op.Responses.ContainsKey("404"), $"{name}: 404 não documentado");
        }
    }

    [Fact]
    public async Task Rotas_que_alteram_estado_documentam_os_conflitos_e_regras_de_negocio()
    {
        var document = await LoadDocumentAsync();
        var ops = Operations(document).ToDictionary(o => $"{o.Method} {o.Path}", o => o.Operation);

        Assert.True(ops["POST /api/assets"].Responses.ContainsKey("409"));
        Assert.True(ops["PUT /api/assets/{id}"].Responses.ContainsKey("409"));
        Assert.True(ops["DELETE /api/assets/{id}"].Responses.ContainsKey("409"));
        Assert.True(ops["DELETE /api/wallets/{id}"].Responses.ContainsKey("409"));
        Assert.True(ops["POST /api/wallets/{walletId}/transactions"].Responses.ContainsKey("422"));
        Assert.True(ops["DELETE /api/wallets/{walletId}/transactions/{id}"].Responses.ContainsKey("422"));
    }

    [Fact]
    public async Task Respostas_de_erro_sao_application_problem_json_e_as_de_sucesso_application_json()
    {
        var document = await LoadDocumentAsync();

        foreach (var (method, path, op) in Operations(document))
            foreach (var (code, response) in op.Responses)
            {
                var name = $"{method} {path} -> {code}";
                if (code.StartsWith('4') || code.StartsWith('5'))
                    Assert.Equal(["application/problem+json"], response.Content.Keys);
                else if (response.Content.Count > 0)
                    Assert.Contains("application/json", response.Content.Keys);
            }
    }

    [Fact]
    public async Task Enums_sao_documentados_como_texto_com_os_valores_validos()
    {
        var document = await LoadDocumentAsync();
        var schemas = document.Components.Schemas;

        Assert.Equal("string", schemas["AssetType"].Type);
        Assert.Equal(["Stock", "RealEstateFund", "Etf", "FixedIncome", "Crypto"],
            schemas["AssetType"].Enum.Select(e => ((Microsoft.OpenApi.Any.OpenApiString)e).Value));
        Assert.Equal(["Buy", "Sell"],
            schemas["TransactionType"].Enum.Select(e => ((Microsoft.OpenApi.Any.OpenApiString)e).Value));
        Assert.False(string.IsNullOrWhiteSpace(schemas["AssetType"].Description));
    }

    [Fact]
    public async Task Corpos_de_requisicao_trazem_exemplo_e_todas_as_propriedades_dos_DTOs_tem_descricao()
    {
        var document = await LoadDocumentAsync();
        var schemas = document.Components.Schemas;

        foreach (var name in new[] { "WalletRequest", "AssetRequest", "TransactionRequest" })
            Assert.NotNull(schemas[name].Example);

        string[] dtos = ["WalletRequest", "WalletResponse", "AssetRequest", "AssetResponse",
                         "TransactionRequest", "TransactionResponse", "PositionResponse"];
        foreach (var dto in dtos)
            foreach (var (prop, schema) in schemas[dto].Properties)
            {
                var ehEnumReferenciado = schema.Reference is not null; // $ref não admite descrição ao lado
                if (!ehEnumReferenciado)
                    Assert.False(string.IsNullOrWhiteSpace(schema.Description), $"{dto}.{prop}: sem descrição");
            }
    }

    [Fact]
    public async Task Swagger_UI_esta_disponivel_em_Development()
    {
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/swagger/index.html")).StatusCode);
    }

    [Fact]
    public async Task Em_producao_o_Swagger_fica_oculto_a_menos_que_Swagger_Enabled_seja_true()
    {
        using var hidden = _factory.WithWebHostBuilder(b => b.UseEnvironment("Production")).CreateClient();
        using var shown = _factory
            .WithWebHostBuilder(b => b.UseEnvironment("Production").UseSetting("Swagger:Enabled", "true"))
            .CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await hidden.GetAsync(SwaggerUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await shown.GetAsync(SwaggerUrl)).StatusCode);
    }
}
