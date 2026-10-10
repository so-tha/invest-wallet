namespace InvestWallet.Application.Exceptions;

/// <summary>Recurso solicitado não existe (vira HTTP 404).</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
