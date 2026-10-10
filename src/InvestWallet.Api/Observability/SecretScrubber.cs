using System.Text.RegularExpressions;

namespace InvestWallet.Api.Observability;

/// <summary>
/// Remove de TEXTOS que saem da aplicação (Sentry, issues do GitHub) os segredos conhecidos e o diretório do usuário
/// do sistema. É uma rede de segurança: não substitui a regra de não colocar segredos em mensagens de exceção.
/// </summary>
public static partial class SecretScrubber
{
    public const string Redacted = "[REDACTED]";

    // Chave sensível + separador (=, : ou aspas do JSON) + valor. O valor pode vir precedido do esquema
    // ("Bearer", "Basic"), e o segredo é o que vem DEPOIS dele. Vai até ';', espaço ou aspas.
    // Exemplos: Password=abc; pwd: xyz; api_key=K; Authorization: Bearer abc; "password":"abc"
    [GeneratedRegex("""(?i)(password|pwd|passwd|secret|token|api[_-]?key|authorization)(["']?\s*[=:]\s*["']?)(?:(?:bearer|basic)\s+)?[^;\s"']+""")]
    private static partial Regex SecretPattern();

    // Diretório do usuário no INÍCIO de um caminho: /home/ana/..., /Users/ana/..., C:\Users\ana\...
    [GeneratedRegex("""^(?:/home/[^/]+|/Users/[^/]+|[A-Za-z]:\\Users\\[^\\]+)""")]
    private static partial Regex UserHomeAtStart();

    // O mesmo, em qualquer ponto de um texto (stack traces trazem caminhos no meio das linhas)
    [GeneratedRegex("""(?:/home/[^/\s]+|/Users/[^/\s]+|[A-Za-z]:\\Users\\[^\\\s]+)""")]
    private static partial Regex UserHomeAnywhere();

    public static string Scrub(string text) => SecretPattern().Replace(text, $"$1$2{Redacted}");

    public static string ScrubPath(string path) => UserHomeAtStart().Replace(path, "~");

    /// <summary>Segredos e diretório do usuário, em qualquer parte do texto.</summary>
    public static string ScrubAll(string text) => UserHomeAnywhere().Replace(Scrub(text), "~");
}
