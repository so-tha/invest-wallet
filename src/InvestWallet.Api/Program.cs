using System.Text.Json.Serialization;
using InvestWallet.Api.Middleware;
using InvestWallet.Api.Swagger;
using InvestWallet.Application;
using InvestWallet.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Serilog.Context;

// Logger mínimo só para registrar falhas de inicialização (ex.: connection string ausente),
// antes de a configuração completa do Serilog existir.
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog substitui o logging padrão e lê níveis/destinos do appsettings.json (seção "Serilog")
    builder.Host.UseSerilog((context, services, logger) => logger
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

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

    builder.Services.AddSwaggerDocumentation();
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // Em containers não há um "dotnet ef" à mão: com a flag ligada, a própria API cria/atualiza o esquema ao subir.
    // Desligada por padrão: em produção real, quem altera o banco deve ser uma decisão de deploy (docs/adr/0004).
    if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        await app.Services.MigrateDatabaseAsync();

    // A ORDEM dos middlewares importa: a requisição passa por eles de cima para baixo e a resposta volta de baixo para cima.

    // 1) Põe o TraceId em TODA linha de log escrita durante esta requisição
    app.Use(async (context, next) =>
    {
        using (LogContext.PushProperty("TraceId", ErrorResponses.GetTraceId(context)))
            await next();
    });

    // 2) Uma linha por requisição: método, rota, status e duração. Fica ANTES do tratador de exceções
    //    para registrar o status final (um erro tratado vira 500 aqui, não uma exceção).
    app.UseSerilogRequestLogging(options =>
    {
        // Health checks são chamados o tempo todo por Docker/orquestradores; não poluem o log
        options.GetLevel = (httpContext, _, ex) =>
            httpContext.Request.Path.StartsWithSegments("/health")
                ? Serilog.Events.LogEventLevel.Verbose
                : ex is not null || httpContext.Response.StatusCode >= 500
                    ? Serilog.Events.LogEventLevel.Error
                    : Serilog.Events.LogEventLevel.Information;
    });

    // 3) Captura exceções de tudo que vem depois
    app.UseExceptionHandler();

    // Swagger sempre em Development; nos demais ambientes só se "Swagger:Enabled=true"
    // (ex.: o container do projeto, para quem for avaliá-lo). Expor a documentação é decisão consciente.
    if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "A aplicação falhou ao iniciar");
    return 1;
}
finally
{
    Log.CloseAndFlush(); // garante que os últimos logs foram escritos antes de encerrar
}

return 0;

// Necessário para os testes de integração (WebApplicationFactory<Program>) enxergarem a classe gerada pelo top-level statements
public partial class Program;
