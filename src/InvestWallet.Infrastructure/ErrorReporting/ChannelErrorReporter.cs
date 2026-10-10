using System.Threading.Channels;
using InvestWallet.Application.ErrorReporting;

namespace InvestWallet.Infrastructure.ErrorReporting;

/// <summary>
/// Fila em memória entre a requisição (que só enfileira, em microssegundos) e o worker (que fala com o GitHub).
/// Limitação conhecida: se o processo cair, o que estava na fila se perde. Para durabilidade real, o próximo
/// passo seria um broker (RabbitMQ, Azure Service Bus).
/// </summary>
public sealed class ChannelErrorReporter : IErrorReporter
{
    private readonly Channel<ErrorReport> _channel;

    public ChannelErrorReporter(int capacity) =>
        _channel = Channel.CreateBounded<ErrorReport>(new BoundedChannelOptions(Math.Max(1, capacity))
        {
            // TryWrite NUNCA bloqueia; com a fila cheia ele devolve false (e o aviso novo é descartado). Com DropWrite o
            // TryWrite devolveria true mesmo descartando, o que faria TryReport mentir sobre ter aceitado o aviso.
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });

    public ChannelReader<ErrorReport> Reader => _channel.Reader;

    public bool TryReport(ErrorReport report) => _channel.Writer.TryWrite(report);
}

/// <summary>Usado quando o aviso de erros está desligado: não aloca fila nem worker.</summary>
public sealed class NullErrorReporter : IErrorReporter
{
    public bool TryReport(ErrorReport report) => false;
}
