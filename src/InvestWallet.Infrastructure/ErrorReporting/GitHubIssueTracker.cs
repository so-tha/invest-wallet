using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InvestWallet.Application.ErrorReporting;

namespace InvestWallet.Infrastructure.ErrorReporting;

/// <summary>
/// Reporta erros como issues do GitHub, sem duplicar: cada erro tem um label <c>error-fp:&lt;impressão digital&gt;</c>.
/// Já existe aberta → comenta; existe fechada → reabre (regressão) e comenta; não existe → cria.
/// O GitHub é a fonte da verdade: reiniciar a API não cria duplicatas.
/// </summary>
public sealed class GitHubIssueTracker : IIssueTracker
{
    private readonly HttpClient _http;
    private readonly IssueTrackingOptions.GitHubOptions _options;

    public GitHubIssueTracker(HttpClient http, IssueTrackingOptions.GitHubOptions options)
    {
        _http = http;
        _options = options;
    }

    private string RepoPath => $"repos/{Uri.EscapeDataString(_options.Owner)}/{Uri.EscapeDataString(_options.Repo)}";

    public async Task ReportAsync(ErrorReport report, int suppressedSinceLast, CancellationToken ct)
    {
        var label = $"error-fp:{report.Fingerprint}";
        var existing = await FindIssueAsync(label, ct);

        if (existing is null)
        {
            await SendAsync(HttpMethod.Post, $"{RepoPath}/issues", new
            {
                title = report.Title,
                body = report.Body,
                labels = _options.Labels.Append(label).ToArray()
            }, ct);
            return;
        }

        var (number, state) = existing.Value;
        var regression = state == "closed";
        if (regression)
            await SendAsync(HttpMethod.Patch, $"{RepoPath}/issues/{number}", new { state = "open" }, ct);

        var comment = $"""
            {(regression ? "**Regressão:** este erro voltou depois de a issue ter sido fechada." : "**Nova ocorrência.**")}

            - Quando (UTC): `{report.OccurredAt.UtcDateTime:yyyy-MM-dd HH:mm:ss}`
            - traceId: `{report.TraceId}`
            {(suppressedSinceLast > 0 ? $"- Ocorrências agrupadas desde o último aviso: **{suppressedSinceLast}**" : "")}
            """;
        await SendAsync(HttpMethod.Post, $"{RepoPath}/issues/{number}/comments", new { body = comment }, ct);
    }

    /// <summary>Busca a issue mais recente (aberta ou fechada) com o label do erro. A API de issues também devolve PRs; ignoramos.</summary>
    private async Task<(int Number, string State)?> FindIssueAsync(string label, CancellationToken ct)
    {
        var url = $"{RepoPath}/issues?state=all&labels={Uri.EscapeDataString(label)}&sort=created&direction=desc&per_page=10";
        using var response = await SendRawAsync(HttpMethod.Get, url, null, ct);
        await EnsureSuccessAsync(response, ct);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        foreach (var item in json.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty("pull_request", out _)) continue;
            return (item.GetProperty("number").GetInt32(), item.GetProperty("state").GetString() ?? "open");
        }
        return null;
    }

    private async Task SendAsync(HttpMethod method, string url, object body, CancellationToken ct)
    {
        using var response = await SendRawAsync(method, url, body, ct);
        await EnsureSuccessAsync(response, ct);
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body);
        try
        {
            return await _http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new IssueTrackerException($"Falha de rede ao falar com o GitHub: {ex.Message}", isTransient: true, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested) // timeout do HttpClient (não é cancelamento do worker)
        {
            throw new IssueTrackerException("Tempo esgotado ao falar com o GitHub.", isTransient: true, ex);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var status = (int)response.StatusCode;
        var rateLimited = response.StatusCode == HttpStatusCode.TooManyRequests
            || (response.StatusCode == HttpStatusCode.Forbidden
                && (response.Headers.Contains("Retry-After")
                    || (response.Headers.TryGetValues("x-ratelimit-remaining", out var remaining) && remaining.FirstOrDefault() == "0")));
        var transient = rateLimited || status >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout;

        // O corpo de erro do GitHub traz um "message" útil; o token nunca aparece nele
        string detail = "";
        try { detail = (await response.Content.ReadAsStringAsync(ct)); } catch { /* sem detalhe */ }
        if (detail.Length > 300) detail = detail[..300];

        var hint = status switch
        {
            401 => " (token inválido ou expirado)",
            403 when !rateLimited => " (o token não tem permissão para escrever issues neste repositório)",
            404 => " (repositório inexistente, ou o token não enxerga o repositório)",
            _ => ""
        };
        throw new IssueTrackerException($"GitHub respondeu {status}{hint}: {detail}", transient);
    }
}
