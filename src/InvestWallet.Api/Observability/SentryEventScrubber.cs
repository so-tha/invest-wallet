using System.Text.RegularExpressions;
using Sentry;
using Sentry.Extensibility;

namespace InvestWallet.Api.Observability;

/// <summary>
/// Remove segredos conhecidos dos eventos ANTES de saírem para o Sentry. As mensagens de exceção são enviadas
/// e podem, por descuido de quem as escreveu ou de uma biblioteca, conter trechos como "Password=...".
/// É uma rede de segurança: não substitui a regra de não colocar segredos em mensagens de exceção.
/// </summary>
public partial class SentryEventScrubber : ISentryEventProcessor
{
    public const string Redacted = "[REDACTED]";

    // password=xxx, pwd: xxx, api_key=xxx, token=xxx, secret=xxx (o valor vai até ';', espaço ou aspas)
    [GeneratedRegex("""(?i)(password|pwd|passwd|secret|token|api[_-]?key|authorization)(\s*[=:]\s*)[^;\s"']+""")]
    private static partial Regex SecretPattern();

    public static string Scrub(string text) => SecretPattern().Replace(text, $"$1$2{Redacted}");

    public SentryEvent? Process(SentryEvent @event)
    {
        if (@event.SentryExceptions is { } exceptions)
            foreach (var exception in exceptions.Where(e => e.Value is not null))
                exception.Value = Scrub(exception.Value!);

        if (@event.Message is { } message)
        {
            if (message.Message is not null) message.Message = Scrub(message.Message);
            if (message.Formatted is not null) message.Formatted = Scrub(message.Formatted);
        }

        return @event;
    }
}
