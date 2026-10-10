using Microsoft.AspNetCore.Mvc;

namespace InvestWallet.Api.Swagger;

/// <summary>Declara uma resposta de erro no formato ProblemDetails (RFC 7807) para o Swagger.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class ProducesProblemResponseAttribute : ProducesResponseTypeAttribute
{
    public ProducesProblemResponseAttribute(int statusCode)
        : base(typeof(ProblemDetails), statusCode, "application/problem+json") { }
}

/// <summary>Declara o 400 com o dicionário <c>errors</c> (campo → mensagens).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ProducesValidationProblemResponseAttribute : ProducesResponseTypeAttribute
{
    public ProducesValidationProblemResponseAttribute()
        : base(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json") { }
}
