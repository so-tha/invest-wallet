namespace InvestWallet.Domain.Exceptions;

/// <summary>Violação de uma regra de negócio ou invariante do domínio.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
