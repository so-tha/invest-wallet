namespace InvestWallet.Application.Exceptions;

/// <summary>A operação conflita com o estado atual (vira HTTP 409).</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
