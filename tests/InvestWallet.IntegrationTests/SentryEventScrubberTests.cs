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

    [Fact]
    public void Evento_sem_excecoes_nem_mensagem_passa_sem_erro()
    {
        var sentryEvent = new SentryEvent();

        Assert.Same(sentryEvent, new SentryEventScrubber().Process(sentryEvent));
    }
}
