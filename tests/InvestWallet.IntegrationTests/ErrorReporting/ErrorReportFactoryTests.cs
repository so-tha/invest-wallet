using InvestWallet.Api.Middleware;
using InvestWallet.Api.Observability;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace InvestWallet.IntegrationTests.ErrorReporting;

public class ErrorReportFactoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeFeature(Exception error, Endpoint? endpoint) : IExceptionHandlerFeature
    {
        public Exception Error { get; } = error;
        public string Path { get; } = "/";
        public Endpoint? Endpoint { get; } = endpoint;
        public RouteValueDictionary? RouteValues { get; } = null;
    }

    private static DefaultHttpContext ContextFor(Exception error, string method = "GET", string routePattern = "api/wallets/{id:guid}")
    {
        var endpoint = new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse(routePattern), 0, null, null);
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = "/api/wallets/" + Guid.NewGuid(); // o caminho REAL muda a cada chamada; o padrão da rota, não
        context.Features.Set<IExceptionHandlerFeature>(new FakeFeature(error, endpoint));
        return context;
    }

    // Lançadores com stack trace real, em métodos distintos (nomes que viram "o ponto do código" do erro)
    private static Exception ThrownFromA(string message)
    {
        try { throw new InvalidOperationException(message); } catch (Exception ex) { return ex; }
    }

    private static Exception ThrownFromB(string message)
    {
        try { throw new InvalidOperationException(message); } catch (Exception ex) { return ex; }
    }

    private static async Task<Exception> ThrownFromAsync()
    {
        await Task.Yield();
        try { throw new InvalidOperationException("async"); } catch (Exception ex) { return ex; }
    }

    private static string Fp(Exception ex, string method = "GET", string route = "api/wallets/{id:guid}") =>
        ErrorReportFactory.Create(ContextFor(ex, method, route), ex, "Production", Now).Fingerprint;

    [Fact]
    public void O_mesmo_erro_no_mesmo_ponto_e_rota_tem_a_mesma_impressao_digital_mesmo_com_mensagem_e_id_diferentes()
    {
        var first = Fp(ThrownFromA("falha para a carteira 111"));
        var second = Fp(ThrownFromA("falha para a carteira 222"));

        Assert.Equal(first, second); // dado variável (id na mensagem, caminho real) não pode gerar uma issue por ocorrência
    }

    [Fact]
    public void Ponto_do_codigo_metodo_http_rota_ou_tipo_diferentes_geram_impressoes_digitais_diferentes()
    {
        var baseline = Fp(ThrownFromA("x"));

        Assert.NotEqual(baseline, Fp(ThrownFromB("x")));                                        // outro ponto do código
        Assert.NotEqual(baseline, Fp(ThrownFromA("x"), method: "POST"));                         // outro verbo
        Assert.NotEqual(baseline, Fp(ThrownFromA("x"), route: "api/assets/{id:guid}"));          // outra rota
        Assert.NotEqual(baseline, Fp(new ArgumentException("x")));                               // outro tipo (sem stack: ponto desconhecido)
    }

    [Fact]
    public void A_impressao_digital_cabe_num_label_do_GitHub()
    {
        var fingerprint = Fp(ThrownFromA("x"));

        Assert.Matches("^[0-9a-f]{12}$", fingerprint);
        Assert.True($"error-fp:{fingerprint}".Length <= 50); // limite de tamanho de label do GitHub
    }

    [Fact]
    public async Task Em_codigo_async_o_ponto_do_erro_e_o_nome_que_o_programador_escreveu_nao_MoveNext()
    {
        var ex = await ThrownFromAsync();

        var report = ErrorReportFactory.Create(ContextFor(ex), ex, "Production", Now);

        Assert.Contains("ErrorReportFactoryTests.ThrownFromAsync", report.Title);
        Assert.DoesNotContain("MoveNext", report.Title);
    }

    [Fact]
    public void O_titulo_traz_tipo_ponto_verbo_e_o_PADRAO_da_rota_e_o_corpo_traz_o_traceId()
    {
        var ex = ThrownFromA("x");
        var context = ContextFor(ex, "POST", "api/wallets/{walletId:guid}/transactions");

        var report = ErrorReportFactory.Create(context, ex, "Production", Now);

        Assert.StartsWith("500 InvalidOperationException em ErrorReportFactoryTests.ThrownFromA", report.Title);
        Assert.Contains("(POST api/wallets/{walletId:guid}/transactions)", report.Title);
        Assert.Contains(ErrorResponses.GetTraceId(context), report.Body);
        Assert.Equal(ErrorResponses.GetTraceId(context), report.TraceId);
        Assert.Contains("2026-06-01 12:00:00", report.Body);
        Assert.Contains("Production", report.Body);
    }

    [Fact]
    public void Segredos_e_o_usuario_do_sistema_nao_entram_no_titulo_nem_no_corpo()
    {
        var ex = ThrownFromA("falha de conexão: Password=senha-super-secreta;Host=db token=abc123");

        var report = ErrorReportFactory.Create(ContextFor(ex), ex, "Production", Now);

        Assert.DoesNotContain("senha-super-secreta", report.Body);
        Assert.DoesNotContain("abc123", report.Body);
        Assert.Contains("Password=[REDACTED]", report.Body);
        Assert.DoesNotContain("/home/", report.Body);   // o stack trace traz caminhos de arquivo com o usuário do sistema
        Assert.DoesNotContain("senha-super-secreta", report.Title);
    }

    [Fact]
    public void Mensagem_enorme_e_stack_trace_enorme_nao_estouram_o_limite_de_65536_caracteres_do_GitHub()
    {
        var ex = ThrownFromA(new string('x', 200_000));

        var report = ErrorReportFactory.Create(ContextFor(ex), ex, "Production", Now);

        Assert.True(report.Body.Length < 20_000, $"corpo com {report.Body.Length} caracteres");
        Assert.True(report.Title.Length <= 200);
    }

    [Fact]
    public void Sem_o_endpoint_original_ainda_gera_o_aviso_com_rota_desconhecida()
    {
        var ex = ThrownFromA("x");
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";

        var report = ErrorReportFactory.Create(context, ex, "Production", Now);

        Assert.Contains("(rota desconhecida)", report.Title);
    }
}
