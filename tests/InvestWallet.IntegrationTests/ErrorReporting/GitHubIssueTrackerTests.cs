using System.Net;
using System.Text.Json;
using InvestWallet.Application.ErrorReporting;
using InvestWallet.Infrastructure.ErrorReporting;

namespace InvestWallet.IntegrationTests.ErrorReporting;

public class GitHubIssueTrackerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly ErrorReport Report = new("abc123def456", "500 X em Y", "corpo do erro", "trace-123", Now);

    private readonly FakeGitHubHandler _github = new();
    private readonly GitHubIssueTracker _tracker;

    public GitHubIssueTrackerTests()
    {
        var options = new IssueTrackingOptions.GitHubOptions { Owner = "ana", Repo = "invest wallet", Token = "ghp_x" };
        _tracker = new GitHubIssueTracker(new HttpClient(_github) { BaseAddress = new Uri("https://api.github.com/") }, options);
    }

    private static HttpResponseMessage Found(string json) => FakeGitHubHandler.Json(HttpStatusCode.OK, json);

    /// <summary>Texto do comentário já decodificado do JSON (o serializador escapa acentos como \u00E3; o GitHub os decodifica).</summary>
    private static string CommentText(FakeGitHubHandler.Call call) =>
        JsonDocument.Parse(call.Body).RootElement.GetProperty("body").GetString()!;

    private void Script(string searchResult) =>
        _github.Responder = call => call.Method == HttpMethod.Get
            ? Found(searchResult)
            : FakeGitHubHandler.Json(HttpStatusCode.OK, """{"number": 1}""");

    [Fact]
    public async Task Erro_novo_busca_pelo_label_e_cria_a_issue_com_os_labels_e_o_corpo()
    {
        await _tracker.ReportAsync(Report, 0, default);

        Assert.Collection(_github.Calls,
            search =>
            {
                Assert.Equal(HttpMethod.Get, search.Method);
                Assert.Contains("labels=error-fp%3Aabc123def456", search.PathAndQuery);
                Assert.Contains("state=all", search.PathAndQuery); // busca também as fechadas, para detectar regressão
                Assert.StartsWith("/repos/ana/invest%20wallet/issues?", search.PathAndQuery); // owner/repo codificados
            },
            create =>
            {
                Assert.Equal(HttpMethod.Post, create.Method);
                using var json = JsonDocument.Parse(create.Body);
                Assert.Equal("500 X em Y", json.RootElement.GetProperty("title").GetString());
                Assert.Equal("corpo do erro", json.RootElement.GetProperty("body").GetString());
                Assert.Equal(["bug", "auto-reported", "error-fp:abc123def456"],
                    json.RootElement.GetProperty("labels").EnumerateArray().Select(l => l.GetString()));
            });
    }

    [Fact]
    public async Task Erro_ja_aberto_comenta_na_issue_existente_e_nao_cria_outra()
    {
        Script("""[{"number": 5, "state": "open"}]""");

        await _tracker.ReportAsync(Report, 0, default);

        Assert.Equal(2, _github.Calls.Count); // busca + comentário
        var comment = _github.Calls[1];
        Assert.Equal(HttpMethod.Post, comment.Method);
        Assert.EndsWith("/issues/5/comments", comment.PathAndQuery);
        Assert.Contains("trace-123", comment.Body);
        Assert.DoesNotContain(_github.Calls, c => c.PathAndQuery.EndsWith("/issues") && c.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task Erro_de_uma_issue_FECHADA_reabre_e_comenta_como_regressao()
    {
        Script("""[{"number": 5, "state": "closed"}]""");

        await _tracker.ReportAsync(Report, 0, default);

        Assert.Equal(3, _github.Calls.Count); // busca, reabrir, comentar
        Assert.Equal(HttpMethod.Patch, _github.Calls[1].Method);
        Assert.EndsWith("/issues/5", _github.Calls[1].PathAndQuery);
        Assert.Contains("\"open\"", _github.Calls[1].Body);
        Assert.Contains("Regressão", CommentText(_github.Calls[2]));
    }

    [Fact]
    public async Task Pull_requests_que_a_API_devolve_junto_com_issues_sao_ignorados()
    {
        Script("""[{"number": 9, "state": "open", "pull_request": {}}, {"number": 4, "state": "open"}]""");

        await _tracker.ReportAsync(Report, 0, default);

        Assert.EndsWith("/issues/4/comments", _github.Calls[1].PathAndQuery);
    }

    [Fact]
    public async Task So_pull_requests_na_busca_significa_que_nao_ha_issue_e_uma_nova_e_criada()
    {
        Script("""[{"number": 9, "state": "open", "pull_request": {}}]""");

        await _tracker.ReportAsync(Report, 0, default);

        Assert.EndsWith("/issues", _github.Calls[1].PathAndQuery);
        Assert.Equal(HttpMethod.Post, _github.Calls[1].Method);
    }

    [Fact]
    public async Task O_comentario_informa_quantas_ocorrencias_foram_agrupadas()
    {
        Script("""[{"number": 5, "state": "open"}]""");

        await _tracker.ReportAsync(Report, suppressedSinceLast: 42, default);

        Assert.Contains("42", CommentText(_github.Calls[1]));
        Assert.Contains("agrupadas", CommentText(_github.Calls[1]));
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(502, true)]
    [InlineData(503, true)]
    [InlineData(429, true)]   // limite de requisições
    [InlineData(408, true)]
    [InlineData(401, false)]  // token inválido: insistir não ajuda
    [InlineData(404, false)]  // repositório inexistente ou invisível para o token
    [InlineData(422, false)]  // requisição rejeitada
    [InlineData(403, false)]  // sem permissão
    public async Task Classifica_as_falhas_em_transitorias_e_permanentes(int status, bool transient)
    {
        _github.Responder = _ => FakeGitHubHandler.Json((HttpStatusCode)status, """{"message":"x"}""");

        var ex = await Assert.ThrowsAsync<IssueTrackerException>(() => _tracker.ReportAsync(Report, 0, default));

        Assert.Equal(transient, ex.IsTransient);
        Assert.Contains(status.ToString(), ex.Message);
    }

    [Fact]
    public async Task Forbidden_por_limite_de_requisicoes_e_transitorio()
    {
        _github.Responder = _ =>
        {
            var response = FakeGitHubHandler.Json(HttpStatusCode.Forbidden, """{"message":"rate limit"}""");
            response.Headers.Add("x-ratelimit-remaining", "0");
            return response;
        };

        var ex = await Assert.ThrowsAsync<IssueTrackerException>(() => _tracker.ReportAsync(Report, 0, default));

        Assert.True(ex.IsTransient);
    }

    [Theory]
    [InlineData(401, "token")]
    [InlineData(404, "repositório")]
    [InlineData(403, "permissão")]
    public async Task A_mensagem_de_falha_permanente_diz_o_que_verificar(int status, string hint)
    {
        _github.Responder = _ => FakeGitHubHandler.Json((HttpStatusCode)status, "{}");

        var ex = await Assert.ThrowsAsync<IssueTrackerException>(() => _tracker.ReportAsync(Report, 0, default));

        Assert.Contains(hint, ex.Message);
    }

    [Fact]
    public async Task Tempo_esgotado_do_HttpClient_e_transitorio_e_diferente_de_cancelamento()
    {
        // O HttpClient sinaliza timeout com TaskCanceledException, mesmo sem o token do chamador ter sido cancelado
        _github.Responder = _ => throw new TaskCanceledException("timeout");

        var ex = await Assert.ThrowsAsync<IssueTrackerException>(() => _tracker.ReportAsync(Report, 0, default));

        Assert.True(ex.IsTransient);
        Assert.Contains("Tempo esgotado", ex.Message);
    }

    [Fact]
    public async Task Falha_de_rede_e_transitoria()
    {
        _github.Responder = _ => throw new HttpRequestException("conexão recusada");

        var ex = await Assert.ThrowsAsync<IssueTrackerException>(() => _tracker.ReportAsync(Report, 0, default));

        Assert.True(ex.IsTransient);
    }

    [Fact]
    public async Task Se_a_criacao_falha_depois_de_a_busca_funcionar_a_falha_e_reportada_e_nao_engolida()
    {
        _github.Responder = call => call.Method == HttpMethod.Get
            ? Found("[]")
            : FakeGitHubHandler.Json(HttpStatusCode.InternalServerError, "{}");

        var ex = await Assert.ThrowsAsync<IssueTrackerException>(() => _tracker.ReportAsync(Report, 0, default));

        Assert.True(ex.IsTransient);
    }
}
