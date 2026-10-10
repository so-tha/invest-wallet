namespace InvestWallet.Infrastructure.ErrorReporting;

/// <summary>Configuração da seção "IssueTracking" (variáveis de ambiente <c>IssueTracking__...</c>).</summary>
public class IssueTrackingOptions
{
    public const string Section = "IssueTracking";

    /// <summary>"None" (padrão, desligado) ou "GitHub".</summary>
    public string Provider { get; set; } = "None";

    /// <summary>Tempo mínimo entre dois avisos do MESMO erro; as repetições nesse intervalo são contadas, não enviadas.</summary>
    public int CooldownMinutes { get; set; } = 10;

    /// <summary>Esperas entre tentativas quando a falha é transitória. O número de itens é o número de retentativas.</summary>
    public TimeSpan[] RetryDelays { get; set; } = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];

    /// <summary>Tamanho máximo da fila; acima disso, novos avisos são descartados (melhor perder um aviso do que travar a API).</summary>
    public int QueueCapacity { get; set; } = 100;

    public GitHubOptions GitHub { get; set; } = new();

    public class GitHubOptions
    {
        public string ApiUrl { get; set; } = "https://api.github.com";
        public string Owner { get; set; } = "";
        public string Repo { get; set; } = "";

        /// <summary>Token com permissão de escrever em issues do repositório. SEGREDO: só por variável de ambiente.</summary>
        public string Token { get; set; } = "";

        public string[] Labels { get; set; } = ["bug", "auto-reported"];
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
    }
}
