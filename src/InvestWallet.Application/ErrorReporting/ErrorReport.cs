namespace InvestWallet.Application.ErrorReporting;

/// <summary>
/// Um erro inesperado (500) pronto para virar issue. O texto já vem sem segredos nem dados pessoais.
/// </summary>
/// <param name="Fingerprint">Identifica "o mesmo erro" (mesmo tipo, mesmo ponto do código, mesma rota): base da deduplicação.</param>
/// <param name="Title">Título curto da issue.</param>
/// <param name="Body">Corpo em Markdown (quando, traceId, rota, exceção, stack trace).</param>
/// <param name="TraceId">Id da requisição, o mesmo da resposta de erro e dos logs.</param>
/// <param name="OccurredAt">Instante do erro.</param>
public record ErrorReport(string Fingerprint, string Title, string Body, string TraceId, DateTimeOffset OccurredAt);
