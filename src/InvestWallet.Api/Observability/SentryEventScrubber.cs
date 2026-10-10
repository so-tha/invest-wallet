using Sentry;
using Sentry.Extensibility;

namespace InvestWallet.Api.Observability;

/// <summary>
/// Remove segredos conhecidos dos eventos ANTES de saírem para o Sentry. As mensagens de exceção são enviadas
/// e podem, por descuido de quem as escreveu ou de uma biblioteca, conter trechos como "Password=...".
/// É uma rede de segurança: não substitui a regra de não colocar segredos em mensagens de exceção.
/// </summary>
public class SentryEventScrubber : ISentryEventProcessor
{
    public const string Redacted = SecretScrubber.Redacted;
    public const string ServerName = "investwallet-api";

    public static string Scrub(string text) => SecretScrubber.Scrub(text);

    public static string ScrubPath(string path) => SecretScrubber.ScrubPath(path);

    public SentryEvent? Process(SentryEvent @event)
    {
        if (@event.SentryExceptions is { } exceptions)
            foreach (var exception in exceptions.Where(e => e.Value is not null))
                exception.Value = Scrub(exception.Value!);

        // O nome de usuário do sistema aparece nos caminhos dos arquivos-fonte (ex.: /home/ana/projeto/...)
        foreach (var frame in (@event.SentryExceptions ?? []).SelectMany(e => e.Stacktrace?.Frames ?? []))
        {
            if (frame.FileName is not null) frame.FileName = ScrubPath(frame.FileName);
            if (frame.AbsolutePath is not null) frame.AbsolutePath = ScrubPath(frame.AbsolutePath);
        }

        // O ASP.NET anexa SERVER_NAME (o nome da máquina) às variáveis de ambiente da requisição
        if (@event.Request?.Env is { } env && env.ContainsKey("SERVER_NAME"))
            env["SERVER_NAME"] = ServerName;

        // Caminhos das DLLs e símbolos carregados (debug_meta): no computador do desenvolvedor passam pelo diretório dele
        foreach (var image in @event.DebugImages ?? [])
        {
            if (image.CodeFile is not null) image.CodeFile = ScrubPath(image.CodeFile);
            if (image.DebugFile is not null) image.DebugFile = ScrubPath(image.DebugFile);
        }

        if (@event.Message is { } message)
        {
            if (message.Message is not null) message.Message = Scrub(message.Message);
            if (message.Formatted is not null) message.Formatted = Scrub(message.Formatted);
        }

        return @event;
    }
}
