using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InvestWallet.Application.Assets;
using InvestWallet.Application.Transactions;
using InvestWallet.Application.Wallets;

namespace InvestWallet.IntegrationTests;

/// <summary>Ajudantes comuns: cliente HTTP, JSON igual ao da API e criação de dados únicos por teste.</summary>
public abstract class ApiTestBase
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected readonly HttpClient Client;

    protected ApiTestBase(ApiFactory factory) => Client = factory.CreateClient();

    protected static string UniqueTicker() => "T" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

    protected async Task<WalletResponse> CreateWalletAsync(string? name = null)
    {
        var response = await Client.PostAsJsonAsync("/api/wallets", new { name = name ?? "Carteira " + Guid.NewGuid().ToString("N")[..6] });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WalletResponse>(Json))!;
    }

    protected async Task<AssetResponse> CreateAssetAsync(string? ticker = null, string type = "Stock")
    {
        var response = await Client.PostAsJsonAsync("/api/assets", new { ticker = ticker ?? UniqueTicker(), name = "Ativo de teste", type });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AssetResponse>(Json))!;
    }

    protected Task<HttpResponseMessage> PostTransactionAsync(
        Guid walletId, Guid assetId, string type, decimal quantity, decimal unitPrice, string executedAt = "2026-01-01T12:00:00Z") =>
        Client.PostAsJsonAsync($"/api/wallets/{walletId}/transactions",
            new { assetId, type, quantity, unitPrice, executedAt });

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    /// <summary>Lê um erro no formato ProblemDetails como JSON genérico.</summary>
    protected static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Json));
}
