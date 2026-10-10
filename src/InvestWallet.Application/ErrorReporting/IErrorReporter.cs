namespace InvestWallet.Application.ErrorReporting;

/// <summary>Porta de entrada para avisar sobre um erro. Nunca bloqueia e nunca lança: avisar não pode piorar o erro.</summary>
public interface IErrorReporter
{
    /// <returns><c>false</c> se o aviso foi descartado (fila cheia ou recurso desligado).</returns>
    bool TryReport(ErrorReport report);
}
