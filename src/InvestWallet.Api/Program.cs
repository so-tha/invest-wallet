using System.Text.Json.Serialization;
using InvestWallet.Api.Middleware;
using InvestWallet.Application;
using InvestWallet.Infrastructure;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers(options =>
    {
        // Campo ausente vira null e é recusado pelo FluentValidation, com a nossa mensagem em português.
        // Sem isso o MVC responderia por conta própria, em inglês e em outro formato.
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;

        // Mensagens do próprio MVC (query string, rota, corpo ausente), traduzidas
        var messages = options.ModelBindingMessageProvider;
        messages.SetAttemptedValueIsInvalidAccessor((value, field) => $"O valor '{value}' não é válido para '{field}'.");
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"O valor '{value}' não é válido.");
        messages.SetValueMustBeANumberAccessor(field => $"O campo '{field}' deve ser um número.");
        messages.SetUnknownValueIsInvalidAccessor(field => $"O valor informado para '{field}' não é válido.");
        messages.SetMissingRequestBodyRequiredValueAccessor(() => "O corpo da requisição é obrigatório.");
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // JSON malformado, tipo errado, enum inexistente, GUID inválido na query...
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = new Dictionary<string, string[]>();
            foreach (var (key, entry) in context.ModelState.Where(e => e.Value?.Errors.Count > 0))
            {
                // Erros "$.campo" vêm do leitor de JSON; a mensagem original expõe detalhes internos
                var isJsonError = key.StartsWith("$");
                var name = ErrorResponses.ToCamelCase(isJsonError ? key.TrimStart('$', '.') : key);
                if (name.Length == 0) name = "body"; // erro que não pertence a um campo específico
                errors[name] = isJsonError
                    ? ["Valor inválido ou em formato incorreto para este campo."]
                    : entry!.Errors.Select(e => e.ErrorMessage).ToArray();
            }

            // "request" é só o eco de "não consegui montar o objeto"; se há erro específico, ele basta
            if (errors.Count > 1) errors.Remove("request");

            return new BadRequestObjectResult(ErrorResponses.CreateValidation(context.HttpContext, errors))
            {
                ContentTypes = { "application/problem+json" }
            };
        };
    })
    // Enums como texto ("Stock") em vez de número (1): mais legível para quem consome a API
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler(); // primeiro do pipeline: captura exceções de tudo que vem depois

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
