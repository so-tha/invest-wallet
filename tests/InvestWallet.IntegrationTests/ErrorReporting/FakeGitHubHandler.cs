using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace InvestWallet.IntegrationTests.ErrorReporting;

/// <summary>Faz o papel do GitHub: grava cada requisição recebida e responde conforme o roteiro do teste. Nunca sai para a rede.</summary>
public sealed class FakeGitHubHandler : HttpMessageHandler
{
    public sealed record Call(HttpMethod Method, string PathAndQuery, string Body, IReadOnlyDictionary<string, string> Headers);

    private readonly ConcurrentQueue<Call> _calls = new();

    public IReadOnlyList<Call> Calls => _calls.ToArray();

    /// <summary>Por padrão: busca devolve lista vazia (200) e escritas devolvem 201.</summary>
    public Func<Call, HttpResponseMessage> Responder { get; set; } = call =>
        call.Method == HttpMethod.Get ? Json(HttpStatusCode.OK, "[]") : Json(HttpStatusCode.Created, """{"number": 1}""");

    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
        var call = new Call(request.Method, request.RequestUri!.PathAndQuery, body, headers);
        _calls.Enqueue(call);
        return Responder(call);
    }
}
