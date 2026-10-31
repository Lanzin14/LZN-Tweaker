namespace LznTweaker.Core.GameTuning;

public sealed record GameConfigProfile(
    string Id,
    string Name,
    string Summary,
    string Details,
    string Author,
    string RiskLevel,
    string? Warning,
    string ResourceFile,
    string SourceCommit,
    DateOnly SourceDate)
{
    /// <summary>Nome do recurso embutido em LznTweaker.Core (GameTuning/Profiles/Deadlock).</summary>
    public string ResourceName => $"LznTweaker.Core.GameTuning.Deadlock.{ResourceFile}";
}

/// <summary>
/// Perfis do Deadlock do projeto OptimizationLock (GPL-3.0), embutidos no app: o conteúdo do gameinfo.gi de cada
/// perfil vai junto e é aplicado sem acessar a internet. Cópia exata dos arquivos do autor; a licença e a origem
/// (commit e data) ficam em GameTuning/Profiles/Deadlock e são copiadas para a pasta do app. O gameinfo.gi muda a
/// cada patch grande do jogo: a comparação de SearchPaths avisa quando a cópia parece desatualizada.
/// </summary>
public static class DeadlockProfiles
{
    public const string SteamAppId = "1422450";

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
            "recommended.gi", "aa2844d4a93f", new DateOnly(2026, 10, 27)),
        new(SqookyMaxFpsId,
            "Sqooky's Max FPS Config",
            "Configuração agressiva, com o maior FPS geral conhecido pelo autor.",
            "Versão Max FPS do OptimizationLock, mantida em uma pasta de testes: o autor diz que ainda não está totalmente documentada " +
            "e que pode causar problemas em alguns computadores, embora normalmente renda mais. Desliga também física de cliente, " +
            "ragdolls e luzes dinâmicas.",
            "Sqooky", "Médio", "Pode apresentar pequenos bugs visuais.",
            "sqooky-maxfps.gi", "aa2844d4a93f", new DateOnly(2026, 10, 27)),
        new(LowEndId,
            "Max FPS (Low-End)",
            "Prioriza FPS acima de tudo, reduzindo drasticamente a qualidade visual. Recomendada apenas para computadores menos potentes.",
            "Config Minimum Spec do Kaizuchaneru: desliga sombras, neblina, céu, iluminação dinâmica, oclusão de ambiente, grama e " +
            "a maior parte do pós-processamento, e reduz texturas. Efeitos conhecidos: heróis ausentes na loja/tela final, contornos " +
            "sólidos e poças com reflexo colorido.",
            "Kaizuchaneru", "Médio", "Reduz muito a qualidade visual; textos e efeitos podem ficar difíceis de ler.",
            "low-end.gi", "347a6694bd77", new DateOnly(2026, 10, 21)),
        new(BootMaxFpsId,
            "Boot's Max FPS (descontinuado)",
            "Max FPS agressivo que o próprio autor do repositório marca como descontinuado (sem manutenção).",
            "Config do Boot: desliga luzes, sombras, neblina, céu, decals e várias animações. O README do OptimizationLock diz que ele " +
            "\"rende bom FPS, mas está funcionalmente descontinuado\", porque o autor não consegue mais mantê-lo. Pode quebrar após " +
            "atualizações do jogo antes dos outros perfis.",
            "Boot", "Alto", "Sem manutenção ativa: maior chance de quebrar após um patch do Deadlock.",
            "boot-maxfps.gi", "4f900690efc2", new DateOnly(2026, 9, 4))
    ];

    public static GameConfigProfile GetById(string id) =>
        All.FirstOrDefault(profile => profile.Id == id) ?? throw new KeyNotFoundException($"Perfil do Deadlock não encontrado: {id}");
}
