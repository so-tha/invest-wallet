using FluentValidation;
using InvestWallet.Application.Exceptions;
using InvestWallet.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace InvestWallet.Api.Middleware;

/// <summary>
/// Converte exceções em respostas HTTP no formato padrão ProblemDetails (RFC 7807).
/// Exceções inesperadas viram 500 sem expor detalhes internos.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        object body;
        int status;

        switch (exception)
        {
            case ValidationException validation: // 400: a requisição em si é inválida
                status = StatusCodes.Status400BadRequest;
                var errors = validation.Errors
                    .GroupBy(e => ErrorResponses.ToCamelCase(e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
                body = ErrorResponses.CreateValidation(context, errors);
                break;

            case NotFoundException:
                status = StatusCodes.Status404NotFound;
                body = ErrorResponses.Create(context, status, "Recurso não encontrado", exception.Message);
                break;

            case ConflictException:
                status = StatusCodes.Status409Conflict;
                _logger.LogWarning("Conflito em {Method} {Path}: {Reason}", context.Request.Method, context.Request.Path, exception.Message);
                body = ErrorResponses.Create(context, status, "Conflito com o estado atual", exception.Message);
                break;

            case DomainException: // 422: a requisição é válida, mas o negócio não permite
                status = StatusCodes.Status422UnprocessableEntity;
                _logger.LogWarning("Regra de negócio violada em {Method} {Path}: {Reason}", context.Request.Method, context.Request.Path, exception.Message);
                body = ErrorResponses.Create(context, status, "Regra de negócio violada", exception.Message);
                break;

            case OperationCanceledException when context.RequestAborted.IsCancellationRequested:
                return true; // o cliente desistiu da requisição: não há ninguém para responder e não é erro nosso

            default: // 500: nunca expomos a mensagem da exceção; ela vai só para o log
                status = StatusCodes.Status500InternalServerError;
                _logger.LogError(exception, "Erro não tratado em {Method} {Path}", context.Request.Method, context.Request.Path);
                body = ErrorResponses.Create(context, status, "Erro interno do servidor",
                    "Ocorreu um erro inesperado. Informe o traceId ao suporte.");
                break;
        }

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(body, body.GetType(), options: null, contentType: "application/problem+json", ct);
        return true; // true = já tratei, não propague
    }
}
