using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace InvestWallet.Api.Swagger;

/// <summary>
/// O <c>[Produces("application/json")]</c> dos controllers força esse tipo em todas as respostas, mas as de erro
/// (4xx/5xx) são <c>application/problem+json</c>. Este filtro corrige o documento para refletir o que a API envia.
/// </summary>
public class ProblemContentTypeOperationFilter : IOperationFilter
{
    private const string Json = "application/json";
    private const string ProblemJson = "application/problem+json";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var (statusCode, response) in operation.Responses)
        {
            if (!int.TryParse(statusCode, out var code) || code < 400) continue;
            if (!response.Content.Remove(Json, out var media)) continue;
            response.Content[ProblemJson] = media;
        }
    }
}
