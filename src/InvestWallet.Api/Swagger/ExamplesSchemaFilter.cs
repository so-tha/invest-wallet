using InvestWallet.Application.Assets;
using InvestWallet.Application.Transactions;
using InvestWallet.Application.Wallets;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace InvestWallet.Api.Swagger;

/// <summary>Põe um exemplo realista nos corpos de requisição, para o botão "Try it out" já vir preenchido.</summary>
public class ExamplesSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(WalletRequest))
            schema.Example = new OpenApiObject
            {
                ["name"] = new OpenApiString("Aposentadoria"),
                ["description"] = new OpenApiString("Investimentos de longo prazo")
            };
        else if (context.Type == typeof(AssetRequest))
            schema.Example = new OpenApiObject
            {
                ["ticker"] = new OpenApiString("PETR4"),
                ["name"] = new OpenApiString("Petrobras PN"),
                ["type"] = new OpenApiString("Stock")
            };
        else if (context.Type == typeof(TransactionRequest))
            schema.Example = new OpenApiObject
            {
                ["assetId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                ["type"] = new OpenApiString("Buy"),
                ["quantity"] = new OpenApiDouble(10),
                ["unitPrice"] = new OpenApiDouble(35.5),
                ["executedAt"] = new OpenApiString("2026-01-15T13:30:00Z")
            };
    }
}
