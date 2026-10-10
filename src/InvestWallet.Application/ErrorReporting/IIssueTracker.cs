namespace InvestWallet.Application.ErrorReporting;

/// <summary>Destino dos avisos (GitHub Issues, Jira...). A aplicação só conhece esta interface.</summary>
public interface IIssueTracker
{
    /// <param name="suppressedSinceLast">Quantas ocorrências iguais foram agrupadas desde o último aviso deste erro.</param>
    /// <exception cref="IssueTrackerException">Falha ao falar com o destino; <see cref="IssueTrackerException.IsTransient"/> diz se vale tentar de novo.</exception>
    Task ReportAsync(ErrorReport report, int suppressedSinceLast, CancellationToken ct);
}

public class IssueTrackerException : Exception
{
    /// <summary>Falha passageira (rede, 5xx, limite de requisições): tentar de novo pode funcionar. Senão (token inválido, repositório inexistente), insistir só atrapalha.</summary>
    public bool IsTransient { get; }

    public IssueTrackerException(string message, bool isTransient, Exception? inner = null) : base(message, inner)
        => IsTransient = isTransient;
}
