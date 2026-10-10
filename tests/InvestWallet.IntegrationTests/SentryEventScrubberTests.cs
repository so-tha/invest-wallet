using InvestWallet.Api.Observability;
using Sentry;
using Sentry.Protocol;

namespace InvestWallet.IntegrationTests;

/// <summary>O limpador é uma rede de segurança: aqui testamos os formatos de segredo mais comuns e que texto comum não é alterado.</summary>
public class SentryEventScrubberTests
{
    [Theory]
    [InlineData("falha: Password=abc123;Host=db", "falha: Password=[REDACTED];Host=db")]
    [InlineData("pwd: xyz rest", "pwd: [REDACTED] rest")]
    [InlineData("api_key=K123 e token=T9", "api_key=[REDACTED] e token=[REDACTED]")]
    [InlineData("API-KEY: abc", "API-KEY: [REDACTED]")]
    [InlineData("secret = valor", "secret = [REDACTED]")]
    [InlineData("Authorization: Bearer abc.def.ghi", "Authorization: [REDACTED]")]   // o token depois de "Bearer" também
    [InlineData("Authorization: Basic dXNlcjpwYXNz", "Authorization: [REDACTED]")]
    [InlineData("{\"password\":\"abc123\",\"user\":\"ana\"}", "{\"password\":\"[REDACTED]\",\"user\":\"ana\"}")]
    public void Remove_o_valor_dos_formatos_de_segredo_mais_comuns(string input, string expected)
    {
        Assert.Equal(expected, SentryEventScrubber.Scrub(input));
    }

    [Theory]
    [InlineData("Falha ao conectar no host db, porta 5432")]
    [InlineData("O token expirou")]                    // a palavra sozinha, sem "=" nem ":", não é segredo
    [InlineData("Venda de 99 excede a posição disponível (10)")]
    [InlineData("tokens: 5")]                          // "tokens" não é "token"
    public void Texto_comum_nao_e_alterado(string input)
    {
        Assert.Equal(input, SentryEventScrubber.Scrub(input));
    }

    [Fact]
    public void Limpa_as_mensagens_das_excecoes_e_a_mensagem_do_evento()
    {
        var sentryEvent = new SentryEvent
        {
            SentryExceptions = [new SentryException { Value = "erro: Password=abc;Host=db" }],
            Message = new SentryMessage { Message = "token=T1", Formatted = "token=T1" }
        };

        new SentryEventScrubber().Process(sentryEvent);

        Assert.Equal("erro: Password=[REDACTED];Host=db", sentryEvent.SentryExceptions!.Single().Value);
        Assert.Equal("token=[REDACTED]", sentryEvent.Message!.Message);
        Assert.Equal("token=[REDACTED]", sentryEvent.Message.Formatted);
    }

    [Theory]
    [InlineData("/home/ana/Documents/projeto/src/Api/Program.cs", "~/Documents/projeto/src/Api/Program.cs")]
    [InlineData("/Users/ana/dev/app/Service.cs", "~/dev/app/Service.cs")]
    [InlineData("C:\\Users\\ana\\source\\app\\Service.cs", "~\\source\\app\\Service.cs")]
    [InlineData("/src/src/InvestWallet.Api/Program.cs", "/src/src/InvestWallet.Api/Program.cs")]   // caminho do build em container: intacto
    [InlineData("/usr/lib/dotnet/shared/Microsoft.AspNetCore.App/8.0.0/x.dll", "/usr/lib/dotnet/shared/Microsoft.AspNetCore.App/8.0.0/x.dll")]
    public void Substitui_o_diretorio_do_usuario_nos_caminhos_e_preserva_os_demais(string input, string expected)
    {
        Assert.Equal(expected, SentryEventScrubber.ScrubPath(input));
    }

    [Fact]
    public void Limpa_o_caminho_dos_arquivos_nos_frames_do_stack_trace()
    {
        var frame = new SentryStackFrame { FileName = "/home/ana/p/Program.cs", AbsolutePath = "/home/ana/p/Program.cs" };
        var sentryEvent = new SentryEvent
        {
            SentryExceptions = [new SentryException { Stacktrace = new SentryStackTrace { Frames = [frame] } }]
        };

        new SentryEventScrubber().Process(sentryEvent);

        Assert.Equal("~/p/Program.cs", frame.FileName);
        Assert.Equal("~/p/Program.cs", frame.AbsolutePath);
    }

    [Fact]
    public void Limpa_SERVER_NAME_da_requisicao_e_os_caminhos_das_imagens_de_debug()
    {
        var sentryEvent = new SentryEvent
        {
            Request = new SentryRequest(),
            DebugImages = [new DebugImage { CodeFile = "/home/ana/app/bin/X.dll", DebugFile = "/home/ana/app/obj/X.pdb" }]
        };
        sentryEvent.Request.Env["SERVER_NAME"] = "notebook-da-ana";
        sentryEvent.Request.Env["SERVER_PORT"] = "5265";

        new SentryEventScrubber().Process(sentryEvent);

        Assert.Equal(SentryEventScrubber.ServerName, sentryEvent.Request.Env["SERVER_NAME"]);
        Assert.Equal("5265", sentryEvent.Request.Env["SERVER_PORT"]); // o resto não é tocado
        var image = Assert.Single(sentryEvent.DebugImages!);
        Assert.Equal("~/app/bin/X.dll", image.CodeFile);
        Assert.Equal("~/app/obj/X.pdb", image.DebugFile);
    }

    [Fact]
    public void Evento_sem_excecoes_nem_mensagem_passa_sem_erro()
    {
        var sentryEvent = new SentryEvent();

        Assert.Same(sentryEvent, new SentryEventScrubber().Process(sentryEvent));
    }
}
