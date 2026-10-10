using InvestWallet.Application.ErrorReporting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace InvestWallet.Infrastructure.ErrorReporting;

public static class ErrorReportingExtensions
{
    /// <summary>
    /// Liga o aviso de erros conforme <c>IssueTracking:Provider</c>. Desligado ("None") por padrão: nada é registrado
    /// e nenhuma chamada externa acontece. Ligado com configuração incompleta, a aplicação RECUSA iniciar com uma
    /// mensagem clara (melhor falhar na subida do que descobrir, no primeiro erro de produção, que o aviso nunca funcionou).
    /// </summary>
    public static IServiceCollection AddErrorReporting(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        var options = new IssueTrackingOptions();
        configuration.GetSection(IssueTrackingOptions.Section).Bind(options);
        services.Configure<IssueTrackingOptions>(configuration.GetSection(IssueTrackingOptions.Section));

        switch (options.Provider.Trim().ToLowerInvariant())
        {
            case "" or "none":
                services.AddSingleton<IErrorReporter, NullErrorReporter>();
                return services;

            case "github":
                ValidateGitHub(options.GitHub);
                var github = options.GitHub;

                services.AddSingleton(github);
                services.AddHttpClient<IIssueTracker, GitHubIssueTracker>(client =>
                {
                    client.BaseAddress = new Uri(github.ApiUrl.TrimEnd('/') + "/");
                    client.Timeout = github.Timeout;
                    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
                    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
                    client.DefaultRequestHeaders.Add("User-Agent", "InvestWallet-ErrorReporter"); // o GitHub exige User-Agent
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", github.Token);
                });

                services.AddSingleton(sp => new ChannelErrorReporter(sp.GetRequiredService<IOptions<IssueTrackingOptions>>().Value.QueueCapacity));
                services.AddSingleton<IErrorReporter>(sp => sp.GetRequiredService<ChannelErrorReporter>());
                services.AddHostedService<ErrorReportingWorker>();
                return services;

            default:
                throw new InvalidOperationException(
                    $"IssueTracking:Provider='{options.Provider}' não é suportado. Use 'None' ou 'GitHub'.");
        }
    }

    private static void ValidateGitHub(IssueTrackingOptions.GitHubOptions github)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(github.Owner)) missing.Add("IssueTracking__GitHub__Owner");
        if (string.IsNullOrWhiteSpace(github.Repo)) missing.Add("IssueTracking__GitHub__Repo");
        if (string.IsNullOrWhiteSpace(github.Token)) missing.Add("IssueTracking__GitHub__Token");
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"IssueTracking:Provider=GitHub exige configuração. Faltando: {string.Join(", ", missing)}.");
    }
}
