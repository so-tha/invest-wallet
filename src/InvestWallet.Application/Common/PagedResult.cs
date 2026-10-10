namespace InvestWallet.Application.Common;

/// <summary>Página de resultados.</summary>
/// <param name="Items">Itens da página.</param>
/// <param name="Page">Número da página devolvida (começa em 1).</param>
/// <param name="PageSize">Tamanho da página efetivamente usado (após normalização).</param>
/// <param name="TotalCount">Total de itens existentes, somando todas as páginas.</param>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
