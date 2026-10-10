using InvestWallet.Application.ErrorReporting;
using InvestWallet.Infrastructure.ErrorReporting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace InvestWallet.IntegrationTests.ErrorReporting;

public class ErrorReportingWorkerTests : IAsyncLifetime
{
    private sealed class ScriptedTracker : IIssueTracker
    {
        private readonly object _lock = new();
        private readonly List<(ErrorReport Report, int Suppressed)> _calls = new();

        /// <summary>Decide o que acontece na N-ésima chamada (1, 2, 3...). Devolver null = sucesso.</summary>
        public Func<int, Exception?> Behavior { get; set; } = _ => null;

        public IReadOnlyList<(ErrorReport Report, int Suppressed)> Calls { get { lock (_lock) return _calls.ToList(); } }

        public Task ReportAsync(ErrorReport report, int suppressedSinceLast, CancellationToken ct)
        {
            int n;
            lock (_lock) { _calls.Add((report, suppressedSinceLast)); n = _calls.Count; }
            var failure = Behavior(n);
            return failure is null ? Task.CompletedTask : Task.FromException(failure);
        }
    }

    private readonly ScriptedTracker _tracker = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly ChannelErrorReporter _queue = new(capacity: 50);
    private readonly ErrorReportingWorker _worker;
    private readonly CancellationTokenSource _cts = new();

    public ErrorReportingWorkerTests()
    {
        var services = new ServiceCollection().AddSingleton<IIssueTracker>(_tracker).BuildServiceProvider();
        var options = Options.Create(new IssueTrackingOptions
        {
            CooldownMinutes = 10,
            RetryDelays = [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero] // sem espera de verdade nos testes
        });
        _worker = new ErrorReportingWorker(_queue, services.GetRequiredService<IServiceScopeFactory>(), options, _clock,
            NullLogger<ErrorReportingWorker>.Instance);
    }

    public async Task InitializeAsync() => await _worker.StartAsync(_cts.Token);

    public async Task DisposeAsync()
    {
        await _cts.CancelAsync();
        await _worker.StopAsync(CancellationToken.None);
    }

    private ErrorReport Report(string fingerprint, string trace = "t") =>
        new(fingerprint, "título", "corpo", trace, _clock.GetUtcNow());

    private static async Task Eventually(Func<bool> condition, string because)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) Assert.Fail($"Condição não atingida em 5 s: {because}");
            await Task.Delay(10);
        }
    }

    /// <summary>Espera o worker "assentar": usado para provar que NADA mais acontece.</summary>
    private static Task Settle() => Task.Delay(150);

    [Fact]
    public async Task Repeticoes_do_mesmo_erro_dentro_do_cooldown_sao_agrupadas_e_contadas()
    {
        for (var i = 0; i < 4; i++) _queue.TryReport(Report("aaa", $"t{i}"));
        await Eventually(() => _tracker.Calls.Count >= 1, "primeiro aviso");
        await Settle();

        Assert.Single(_tracker.Calls); // 4 ocorrências, 1 aviso

        _clock.Advance(TimeSpan.FromMinutes(11)); // passou o cooldown
        _queue.TryReport(Report("aaa", "t-depois"));
        await Eventually(() => _tracker.Calls.Count == 2, "segundo aviso depois do cooldown");

        Assert.Equal(3, _tracker.Calls[1].Suppressed); // as 3 repetições agrupadas vão junto, no novo aviso
        Assert.Equal(0, _tracker.Calls[0].Suppressed);
    }

    [Fact]
    public async Task Erros_diferentes_nao_se_agrupam()
    {
        _queue.TryReport(Report("aaa"));
        _queue.TryReport(Report("bbb"));

        await Eventually(() => _tracker.Calls.Count == 2, "dois erros diferentes, dois avisos");
    }

    [Fact]
    public async Task Falha_transitoria_e_repetida_ate_dar_certo()
    {
        _tracker.Behavior = n => n <= 2 ? new IssueTrackerException("GitHub fora do ar", isTransient: true) : null;

        _queue.TryReport(Report("aaa"));

        await Eventually(() => _tracker.Calls.Count == 3, "duas falhas e um sucesso");
        await Settle();
        Assert.Equal(3, _tracker.Calls.Count);
    }

    [Fact]
    public async Task Falha_transitoria_persistente_desiste_depois_das_tentativas_e_o_worker_continua()
    {
        _tracker.Behavior = _ => new IssueTrackerException("fora do ar", isTransient: true);

        _queue.TryReport(Report("aaa"));
        await Eventually(() => _tracker.Calls.Count == 4, "1 tentativa + 3 retentativas");
        await Settle();
        Assert.Equal(4, _tracker.Calls.Count); // para; não tenta para sempre

        _tracker.Behavior = _ => null;
        _queue.TryReport(Report("bbb")); // o worker segue vivo e atende o próximo
        await Eventually(() => _tracker.Calls.Count == 5, "worker continua processando");
    }

    [Fact]
    public async Task Falha_permanente_nao_e_repetida()
    {
        _tracker.Behavior = _ => new IssueTrackerException("token inválido", isTransient: false);

        _queue.TryReport(Report("aaa"));
        await Eventually(() => _tracker.Calls.Count >= 1, "uma tentativa");
        await Settle();

        Assert.Single(_tracker.Calls); // insistir com token inválido só atrapalha
    }

    [Fact]
    public async Task Excecao_inesperada_do_destino_nao_mata_o_worker()
    {
        _tracker.Behavior = n => n == 1 ? new InvalidOperationException("bug no adaptador") : null;

        _queue.TryReport(Report("aaa"));
        await Eventually(() => _tracker.Calls.Count == 1, "primeira chamada (explode)");

        _queue.TryReport(Report("bbb"));
        await Eventually(() => _tracker.Calls.Count == 2, "o worker continua vivo depois da exceção");
    }

    [Fact]
    public async Task Falha_do_destino_nao_impede_o_cooldown_de_proteger_o_destino()
    {
        // GitHub fora do ar: 50 ocorrências do mesmo erro não podem virar 50 rajadas de tentativas
        _tracker.Behavior = _ => new IssueTrackerException("fora do ar", isTransient: false);

        for (var i = 0; i < 50; i++) _queue.TryReport(Report("aaa", $"t{i}"));
        await Eventually(() => _tracker.Calls.Count >= 1, "primeira tentativa");
        await Settle();

        Assert.Single(_tracker.Calls);
    }

    [Fact]
    public void Fila_cheia_descarta_o_novo_aviso_sem_bloquear_quem_reporta()
    {
        var small = new ChannelErrorReporter(capacity: 2); // sem worker consumindo

        Assert.True(small.TryReport(Report("a")));
        Assert.True(small.TryReport(Report("b")));
        Assert.False(small.TryReport(Report("c"))); // descartado, e a chamada retornou na hora
    }
}
