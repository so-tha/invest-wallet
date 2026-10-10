using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using InvestWallet.Api.Middleware;
using InvestWallet.Application.ErrorReporting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace InvestWallet.Api.Observability;

/// <summary>Monta o aviso de um erro 500: título, corpo sem dados sensíveis e a impressão digital que identifica "o mesmo erro".</summary>
public static class ErrorReportFactory
{
    private const int MaxStackTraceChars = 8_000;
    private const int MaxTitleChars = 200;
    private const int MaxMessageChars = 1_000;

    public static ErrorReport Create(HttpContext context, Exception exception, string environment, DateTimeOffset now)
    {
        var traceId = ErrorResponses.GetTraceId(context);
        var method = context.Request.Method;

        // O ExceptionHandlerMiddleware limpa o endpoint do contexto antes de nos chamar; o original fica nesta feature.
        // Usamos o PADRÃO da rota ("api/wallets/{walletId:guid}/transactions"), não o caminho real: assim, o mesmo erro
        // em carteiras diferentes gera a mesma impressão digital, e não uma issue por id.
        var route = (context.Features.Get<IExceptionHandlerFeature>()?.Endpoint as RouteEndpoint)?.RoutePattern.RawText
                    ?? "(rota desconhecida)";

        var root = exception.GetBaseException();
        var site = FindThrowSite(exception);
        var fingerprint = Fingerprint($"{root.GetType().FullName}|{site}|{method} {route}");

        var title = Truncate($"500 {root.GetType().Name} em {site} ({method} {route})", MaxTitleChars);

        var message = Truncate(SecretScrubber.ScrubAll(root.Message).ReplaceLineEndings(" "), MaxMessageChars);
        var stack = SecretScrubber.ScrubAll(exception.ToString());
        if (stack.Length > MaxStackTraceChars) stack = stack[..MaxStackTraceChars] + "\n... (truncado)";

        var body = $"""
            **Erro 500 detectado automaticamente.** Comentários nesta issue são novas ocorrências; fechá-la e o erro voltar a reabre.

            | | |
            |---|---|
            | Quando (UTC) | `{now.UtcDateTime:yyyy-MM-dd HH:mm:ss}` |
            | traceId | `{traceId}` |
            | Requisição | `{method} {route}` |
            | Ambiente | `{environment}` |
            | Exceção | `{root.GetType().FullName}` |
            | Onde | `{site}` |

            **Mensagem:** {message}

            <details><summary>Stack trace</summary>

            ```
            {stack}
            ```

            </details>

            Para investigar: procure o `traceId` acima nos logs (todas as linhas da requisição o carregam).
            """;

        return new ErrorReport(fingerprint, title, body, traceId, now);
    }

    /// <summary>12 caracteres hexadecimais de um SHA-256: curto o bastante para um label do GitHub (limite de 50 caracteres).</summary>
    private static string Fingerprint(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12].ToLowerInvariant();

    /// <summary>
    /// "Onde, no NOSSO código, isto aconteceu": o primeiro frame de código da aplicação, procurando da exceção mais externa
    /// para a mais interna. As exceções externas já carregam em seu stack trace os métodos nossos por onde o erro subiu,
    /// mesmo quando a causa raiz está dentro de uma biblioteca (Npgsql, EF).
    /// </summary>
    private static string FindThrowSite(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var frames = new StackTrace(current, fNeedFileInfo: false).GetFrames();
            foreach (var frame in frames)
            {
                var method = frame.GetMethod();
                if (method?.DeclaringType?.Namespace?.StartsWith("InvestWallet", StringComparison.Ordinal) == true)
                    return Describe(method);
            }
        }
        return "(código da aplicação não identificado)";
    }

    /// <summary>
    /// Nome legível "Classe.Método". Em código async/lambda o compilador gera classes e métodos com nomes como
    /// <c>WalletService+&lt;ListAsync&gt;d__3.MoveNext</c>; aqui voltamos ao nome que o programador escreveu.
    /// </summary>
    private static string Describe(MethodBase method)
    {
        var type = method.DeclaringType!;
        var name = method.Name;

        if (type.Name.StartsWith('<') && type.Name.Contains(">d__")) // máquina de estados de async: <Método>d__N
            name = type.Name[1..type.Name.IndexOf('>')];
        else if (name.StartsWith('<'))                                 // lambda/função local: <Método>b__N
            name = name[1..name.IndexOf('>')];

        while (type.IsNested && (type.Name.Contains('<') || type.Name.Contains('>')))
            type = type.DeclaringType!;

        return $"{type.Name}.{name}";
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
