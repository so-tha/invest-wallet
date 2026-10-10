using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InvestWallet.Api.Middleware;

/// <summary>Monta todas as respostas de erro no mesmo formato (RFC 7807), sempre com traceId.</summary>
public static class ErrorResponses
{
    /// <summary>
    /// Id que liga a resposta ao log. Usa o TraceId do padrão W3C (32 hex, o mesmo que aparece em outras
    /// ferramentas de observabilidade); cai para o id da requisição se não houver Activity.
    /// </summary>
    public static string GetTraceId(HttpContext context) =>
        Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

    public static ProblemDetails Create(HttpContext context, int status, string title, string detail)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail, Instance = context.Request.Path };
        problem.Extensions["traceId"] = GetTraceId(context);
        return problem;
    }

    public static ValidationProblemDetails CreateValidation(HttpContext context, IDictionary<string, string[]> errors)
    {
        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Dados de entrada inválidos",
            Detail = "Um ou mais campos são inválidos. Veja 'errors' para os detalhes.",
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = GetTraceId(context);
        return problem;
    }

    /// <summary>"Name" → "name": o JSON da API usa camelCase, as chaves de erro devem seguir o mesmo padrão.</summary>
    public static string ToCamelCase(string key) =>
        string.IsNullOrEmpty(key) ? key : char.ToLowerInvariant(key[0]) + key[1..];
}
