using Microsoft.OpenApi.Models;

namespace InvestWallet.Api.Swagger;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "InvestWallet API",
                Version = "v1",
                Description = """
                    API REST para registrar carteiras de investimento, ativos e transações de compra/venda,
                    calculando a **posição** atual e o **preço médio** de cada ativo.

                    **Erros.** Toda resposta de erro usa `application/problem+json` (RFC 7807) e traz um `traceId`
                    que permite localizar a requisição nos logs:
                    - `400`: requisição inválida (campo ausente, formato ou limite incorreto); detalhes em `errors`;
                    - `404`: recurso da URL não existe;
                    - `409`: conflito com o estado atual (ticker duplicado, excluir algo que possui transações);
                    - `422`: regra de negócio violada (ex.: vender mais do que se possui);
                    - `500`: erro inesperado (a mensagem é genérica; o detalhe fica só no log).
                    """
            });

            // Lê os comentários /// do código (gerados em arquivos .xml a cada build)
            foreach (var xml in Directory.GetFiles(AppContext.BaseDirectory, "InvestWallet.*.xml"))
                options.IncludeXmlComments(xml, includeControllerXmlComments: true);

            options.SchemaFilter<ExamplesSchemaFilter>();
            options.OperationFilter<ProblemContentTypeOperationFilter>();
        });
        return services;
    }
}
