using InvestWallet.Application.ErrorReporting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvestWallet.Infrastructure.ErrorReporting;

/// <summary>
/// Consome a fila em segundo plano: agrupa repetições (cooldown por impressão digital), tenta de novo em falhas
/// transitórias e NUNCA deixa uma falha do destino derrubar a aplicação.
/// </summary>
public sealed class ErrorReportingWorker : BackgroundService
{
    private readonly ChannelErrorReporter _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<IssueTrackingOptions> _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<ErrorReportingWorker> _logger;

    // Estado só tocado por este worker (um único leitor), então não precisa de lock
    private readonly Dictionary<string, (DateTimeOffset LastSent, int Suppressed)> _seen = new();

    public ErrorReportingWorker(
        ChannelErrorReporter queue, IServiceScopeFactory scopes, IOptions<IssueTrackingOptions> options,
        TimeProvider clock, ILogger<ErrorReportingWorker> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var report in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessAsync(report, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex) // rede de segurança final: este laço não pode morrer
                {
                    _logger.LogError(ex, "Falha inesperada ao processar o aviso de erro {Fingerprint}", report.Fingerprint);
                }
            }
        }
        catch (OperationCanceledException) { /* encerrando a aplicação */ }
    }

    private async Task ProcessAsync(ErrorReport report, CancellationToken ct)
    {
        var options = _options.Value;
        var now = _clock.GetUtcNow();
        var cooldown = TimeSpan.FromMinutes(options.CooldownMinutes);

        var suppressed = 0;
        if (_seen.TryGetValue(report.Fingerprint, out var previous))
        {
            if (now - previous.LastSent < cooldown)
            {
                _seen[report.Fingerprint] = (previous.LastSent, previous.Suppressed + 1);
                _logger.LogDebug("Erro {Fingerprint} repetido dentro do intervalo: agrupado", report.Fingerprint);
                return;
            }
            suppressed = previous.Suppressed;
        }

        // O cooldown começa na tentativa (e não só no sucesso): se o GitHub estiver fora do ar, não o
        // martelamos uma vez para cada ocorrência do mesmo erro.
        _seen[report.Fingerprint] = (now, 0);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var tracker = scope.ServiceProvider.GetRequiredService<IIssueTracker>();
                await tracker.ReportAsync(report, suppressed, ct);
                _logger.LogInformation("Erro {Fingerprint} (traceId {TraceId}) reportado", report.Fingerprint, report.TraceId);
                return;
            }
            catch (IssueTrackerException ex) when (ex.IsTransient && attempt < options.RetryDelays.Length)
            {
                _logger.LogWarning("Falha transitória ao reportar o erro {Fingerprint} (tentativa {Attempt}): {Reason}",
                    report.Fingerprint, attempt + 1, ex.Message);
                await Task.Delay(options.RetryDelays[attempt], _clock, ct);
            }
            catch (IssueTrackerException ex)
            {
                // Permanente (token inválido, repositório inexistente...) ou tentativas esgotadas: desistir e avisar no log
                _logger.LogError("Não foi possível reportar o erro {Fingerprint} (traceId {TraceId}): {Reason}",
                    report.Fingerprint, report.TraceId, ex.Message);
                return;
            }
        }
    }
}
