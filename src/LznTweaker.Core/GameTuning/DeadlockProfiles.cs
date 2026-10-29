namespace LznTweaker.Core.GameTuning;

public sealed record GameConfigProfile(
    string Id,
    string Name,
    string Summary,
    string Details,
    string Author,
    string RiskLevel,
    string? Warning,
    string RepositoryPath)
{
    /// <summary>Link para a página do arquivo no GitHub (para o usuário conferir a origem).</summary>
    public string WebUrl => $"{DeadlockProfiles.RepositoryWebUrl}/blob/main/{Uri.EscapeDataString(RepositoryPath).Replace("%2F", "/")}";
}

/// <summary>
/// Perfis do Deadlock vindos do projeto OptimizationLock (GPL-3.0). Os arquivos não são embutidos no app:
/// são baixados do repositório do autor na hora de aplicar, para não redistribuir conteúdo GPL e para
/// sempre usar a versão ajustada ao patch atual do jogo (o gameinfo.gi muda a cada atualização grande).
/// </summary>
public static class DeadlockProfiles
{
    public const string SteamAppId = "1422450";
    public const string RepositoryWebUrl = "https://github.com/Sqooky/OptimizationLock";
    internal const string RawBaseUrl = "https://raw.githubusercontent.com/Sqooky/OptimizationLock/main/";
    internal const string CommitsApiUrl = "https://api.github.com/repos/Sqooky/OptimizationLock/commits";

    public const string RecommendedId = "deadlock-recommended";
    public const string SqookyMaxFpsId = "deadlock-sqooky-maxfps";
    public const string LowEndId = "deadlock-low-end";
    public const string BootMaxFpsId = "deadlock-boot-maxfps";

    public static IReadOnlyList<GameConfigProfile> All { get; } =
    [
        new(RecommendedId,
            "Recomendada",
            "Equilíbrio entre desempenho e visual. É a indicação do autor para a maioria dos usuários.",
            "Config padrão do OptimizationLock (Sqooky): reduz sombras e luzes dinâmicas, pós-processamento, partículas e efeitos de " +
            "câmera, tentando preservar ao máximo a fidelidade visual. Efeitos conhecidos: prédios aparecendo de repente à distância " +
            "e retratos de heróis mais escuros.",
            "Sqooky", "Baixo", null,
            "Sqooky's .gi/gameinfo.gi"),
        new(SqookyMaxFpsId,
            "Sqooky's Max FPS Config",
            "Configuração agressiva, com o maior FPS geral conhecido pelo autor.",
            "Versão Max FPS do OptimizationLock, mantida em uma pasta de testes: o autor diz que ainda não está totalmente documentada " +
            "e que pode causar problemas em alguns computadores, embora normalmente renda mais. Desliga também física de cliente, " +
            "ragdolls e luzes dinâmicas.",
            "Sqooky", "Médio", "Pode apresentar pequenos bugs visuais.",
            "maximumfps or minimum spec config and resources here/gameinfo.gi"),
        new(LowEndId,
            "Max FPS (Low-End)",
            "Prioriza FPS acima de tudo, reduzindo drasticamente a qualidade visual. Recomendada apenas para computadores menos potentes.",
            "Config Minimum Spec do Kaizuchaneru: desliga sombras, neblina, céu, iluminação dinâmica, oclusão de ambiente, grama e " +
            "a maior parte do pós-processamento, e reduz texturas. Efeitos conhecidos: heróis ausentes na loja/tela final, contornos " +
            "sólidos e poças com reflexo colorido.",
            "Kaizuchaneru", "Médio", "Reduz muito a qualidade visual; textos e efeitos podem ficar difíceis de ler.",
            "kaizuchanerus minimum spec/gameinfo.gi"),
        new(BootMaxFpsId,
            "Boot's Max FPS (descontinuado)",
            "Max FPS agressivo que o próprio autor do repositório marca como descontinuado (sem manutenção).",
            "Config do Boot: desliga luzes, sombras, neblina, céu, decals e várias animações. O README do OptimizationLock diz que ele " +
            "\"rende bom FPS, mas está funcionalmente descontinuado\", porque o autor não consegue mais mantê-lo. Pode quebrar após " +
            "atualizações do jogo antes dos outros perfis.",
            "Boot", "Alto", "Sem manutenção ativa: maior chance de quebrar após um patch do Deadlock.",
            "boot's maxium fps config/gameinfo.gi")
    ];

    public static GameConfigProfile GetById(string id) =>
        All.FirstOrDefault(profile => profile.Id == id) ?? throw new KeyNotFoundException($"Perfil do Deadlock não encontrado: {id}");
}
