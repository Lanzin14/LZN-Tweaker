namespace LznTweaker.Core.GameOptimization;

/// <summary>De onde vem uma recomendação: separa o que é oficial do que é técnica, relato da comunidade ou teste.</summary>
public enum EvidenceKind
{
    /// <summary>Publicado pela Epic, NVIDIA ou AMD.</summary>
    Official,
    /// <summary>Consequência técnica conhecida do recurso, sem recomendação oficial específica para o Fortnite.</summary>
    Technical,
    /// <summary>Relato recorrente da comunidade competitiva; não confirmado oficialmente.</summary>
    Community,
    /// <summary>Teste por conta do usuário: compare antes e depois e volte se piorar.</summary>
    Experimental
}

/// <param name="Note">Condição, alternativa ou aviso; nulo quando a linha se explica sozinha.</param>
public sealed record RecommendedSetting(string Name, string Value, string Purpose, EvidenceKind Evidence, string? Note = null);

/// <summary>
/// Recomendações do Fortnite (docs/OTIMIZACAO-POR-JOGO.md, seção H). São instruções manuais: o LZN Tweaker não edita
/// arquivos do jogo, não altera o Epic Games Launcher, não identifica a placa de vídeo para escolher a seção (o usuário
/// escolhe NVIDIA ou AMD) e nunca toca no Easy Anti-Cheat ou no BattlEye. Nomes conferidos em 31/10/2026 no artigo
/// oficial da Epic "Fortnite para PC: melhores configurações para jogo competitivo em 2026" (30/04/2026) e nas páginas
/// de ajuda da Epic, da NVIDIA e da AMD.
/// </summary>
public static class FortniteRecommendations
{
    /// <summary>Última atualização deste conteúdo; aparece na aba Configurações porque patches mudam menus e opções.</summary>
    public static DateOnly UpdatedOn { get; } = new(2026, 10, 31);

    public const string VideoMenuPath = "Fortnite > ícone do jogador > Configurações (engrenagem) > Vídeo";
    public const string NvidiaMenuPath = "Painel de Controle NVIDIA > Gerenciar as configurações em 3D > Configurações de programa > Fortnite (FortniteClient-Win64-Shipping.exe)";
    public const string AmdMenuPath = "AMD Software: Adrenalin Edition > Jogos > Jogos > Fortnite (perfil do jogo; adicione o jogo se ele não aparecer)";

    /// <summary>Perfil competitivo: Modo de Renderização "Desempenho", que hoje usa o DirectX 12 (Epic, 2026).</summary>
    public static IReadOnlyList<RecommendedSetting> PerformanceMode { get; } =
    [
        new("Modo de Janela", "Tela Cheia", "Deixa o Fortnite como foco do PC e reduz o atraso de entrada.", EvidenceKind.Official),
        new("Resolução", "Nativa do monitor", "Imagem nítida, sem escala. Muitos competitivos usam 1920×1080.", EvidenceKind.Official),
        new("Vsync", "Sem", "Evita o atraso de entrada do V-Sync.", EvidenceKind.Official,
            "Para evitar rasgos na tela, use um monitor FreeSync (AMD) ou G-Sync (NVIDIA)."),
        new("Modo de Renderização", "Desempenho – Menor fidelidade aos gráficos", "Desativa a maioria dos recursos de renderização para aliviar CPU, GPU e memória.",
            EvidenceKind.Official, "Selecione Aplicar e reinicie o Fortnite para valer."),
        new("Limite da Taxa de Quadros", "Ilimitado", "Deixa o jogo entregar o máximo de quadros.", EvidenceKind.Official,
            "Se notar rasgos na tela ou FPS muito instável, limite na taxa de atualização do monitor."),
        new("Resolução 3D", "100%", "Resolução em que os objetos 3D são renderizados.", EvidenceKind.Official,
            "Se o FPS não for suficiente, reduza em etapas de 10% até chegar ao desempenho desejado; cada etapa deixa a imagem menos nítida."),
        new("Distância de Visão", "Perto", "Renderiza só o entorno imediato; os jogadores continuam nítidos e visíveis.", EvidenceKind.Official),
        new("Textura", "Baixa", "Prioriza o desempenho e o uso de memória de vídeo.", EvidenceKind.Official),
        new("Malhas", "Baixa", "Objetos com menos detalhes, otimizando o desempenho.", EvidenceKind.Official),
        new("Exibir FPS", "Com", "Mostra o desempenho em tempo real para comparar os ajustes.", EvidenceKind.Official),
        new("Baixa Latência do NVIDIA Reflex", "Ligado + Boost", "Reduz a latência da placa de vídeo.", EvidenceKind.Official,
            "Só com placa NVIDIA. O Boost mantém os clocks altos e aumenta o consumo."),
    ];

    /// <summary>Alternativa para quem tem travadas, instabilidade ou imagem ruim no Modo Desempenho.</summary>
    public static IReadOnlyList<RecommendedSetting> DirectX12 { get; } =
    [
        new("Modo de Renderização", "DirectX 12", "Renderizador completo; alternativa ao Modo Desempenho, não um substituto melhor para todos.",
            EvidenceKind.Technical, "Selecione Aplicar e reinicie o Fortnite para valer."),
        new("Modo de Janela", "Tela Cheia", "Menor atraso de entrada.", EvidenceKind.Official),
        new("Resolução", "Nativa do monitor", "Imagem nítida, sem escala.", EvidenceKind.Official),
        new("Vsync", "Sem", "Evita o atraso de entrada do V-Sync.", EvidenceKind.Official),
        new("Resolução 3D", "100%", "Mantém a nitidez; reduza em etapas de 10% só se faltar FPS.", EvidenceKind.Official),
        new("Resolução 3D Dinâmica", "Desativada", "Evita que a nitidez mude no meio da partida.", EvidenceKind.Technical),
        new("Nanite (geometria virtualizada)", "Desativado", "Recurso pesado de detalhe geométrico.", EvidenceKind.Technical),
        new("Sombras", "Desativadas", "Sombras pesam muito no DirectX 12.", EvidenceKind.Technical),
        new("Iluminação Global", "Desativada", "Remove a iluminação indireta, que é cara.", EvidenceKind.Technical),
        new("Reflexos", "Desativados", "Remove os reflexos, que pesam e não ajudam na leitura da cena.", EvidenceKind.Technical),
        new("Distância de Visão", "Perto", "Renderiza só o entorno imediato.", EvidenceKind.Official),
        new("Texturas", "Baixa", "Alivia a memória de vídeo.", EvidenceKind.Technical),
        new("Efeitos", "Baixa", "Explosões e partículas mais leves.", EvidenceKind.Technical),
        new("Pós-processamento", "Baixa", "Menos efeitos de imagem sobre a cena.", EvidenceKind.Technical),
        new("Ray Tracing por hardware", "Desativado", "Traçado de raios é o recurso mais pesado.", EvidenceKind.Official),
        new("Desfoque de Movimento", "Desativado", "Mantém a imagem nítida em movimento.", EvidenceKind.Technical),
        new("Baixa Latência do NVIDIA Reflex", "Ligado + Boost", "Reduz a latência da placa de vídeo.", EvidenceKind.Official, "Só com placa NVIDIA."),
    ];

    public static IReadOnlyList<string> VideoNotes { get; } =
    [
        "O DirectX 12 não é superior ao Modo Desempenho para todos. Teste os dois em partidas reais ou no Modo Criativo e compare estabilidade, 1% low, frametime e sensação de resposta.",
        "Relatos da comunidade (Reddit, 2025–2026) são contraditórios: o Modo Desempenho costuma dar FPS médio maior; o DirectX 12 às vezes dá 1% low mais estável em CPUs com mais núcleos.",
        "As primeiras partidas depois de trocar o modo ou atualizar o driver podem travar enquanto os shaders são compilados.",
        "Os nomes das opções do DirectX 12 podem variar um pouco entre versões do jogo.",
    ];

    /// <summary>Painel de Controle NVIDIA, no perfil do Fortnite.</summary>
    public static IReadOnlyList<RecommendedSetting> Nvidia { get; } =
    [
        new("NVIDIA Reflex (no jogo)", "Ligado + Boost", "Reduz a latência; fica nas configurações de vídeo do Fortnite, não no painel.", EvidenceKind.Official),
        new("Modo de baixa latência", "Desligado", "Com o Reflex ligado no jogo, o Reflex tem prioridade e substitui esta opção; deixar as duas ligadas é redundante.",
            EvidenceKind.Official, "Só use esta opção em jogos sem Reflex."),
        new("Modo de gerenciamento de energia", "Preferir desempenho máximo", "Mantém a placa em clocks altos enquanto o Fortnite está aberto.",
            EvidenceKind.Technical, "Aumenta o consumo e o calor. Defina só no perfil do Fortnite, não nas configurações globais."),
        new("Sincronização vertical", "Desligado", "Configuração competitiva inicial: sem a latência do V-Sync.", EvidenceKind.Technical,
            "Com monitor G-SYNC, a comunidade usa G-SYNC + Sincronização vertical ligada no painel + Reflex, que limita o FPS logo abaixo da taxa do monitor (relato da comunidade, não recomendação oficial)."),
        new("Tamanho do cache de shader", "Padrão do driver", "Guarda os shaders compilados e reduz travadas ao reabrir o jogo.", EvidenceKind.Technical,
            "Não existe tamanho universal; aumente só se tiver espaço sobrando e notar recompilação frequente."),
        new("Filtragem de textura - Qualidade", "Alto desempenho", "Troca um pouco de qualidade de textura por desempenho.", EvidenceKind.Experimental,
            "Opção de teste: compare antes e depois e volte para Qualidade se não notar diferença."),
    ];

    /// <summary>AMD Software: Adrenalin Edition, no perfil do Fortnite.</summary>
    public static IReadOnlyList<RecommendedSetting> Amd { get; } =
    [
        new("Perfil do jogo", "Criar para o Fortnite", "As opções abaixo valem só para o Fortnite e não mudam os outros jogos.", EvidenceKind.Official,
            "Em Jogos, abra o Fortnite; se ele não aparecer, adicione pelo menu de três pontos (Adicionar um jogo) apontando para o executável."),
        new("Radeon Anti-Lag", "Ativado", "Reduz a latência de entrada no driver.", EvidenceKind.Official,
            "O Radeon Anti-Lag 2 exige suporte do próprio jogo, e o Fortnite não aparece na lista da AMD consultada em 31/10/2026. Se um dia a opção surgir no menu do jogo, prefira a do jogo. Nunca use o antigo Anti-Lag+."),
        new("Radeon Chill", "Desativado", "Evita a limitação dinâmica de FPS.", EvidenceKind.Official,
            "A AMD documenta que Chill e Anti-Lag não ficam ativos juntos: ligar um desliga o outro."),
        new("Aguardar sincronização vertical", "Desativado, a menos que o aplicativo especifique", "Deixa o Fortnite decidir; com o Vsync do jogo em Sem, fica sem V-Sync.",
            EvidenceKind.Technical),
        new("AMD FreeSync", "AMD Otimizado", "Valor padrão do perfil; sincroniza a taxa do monitor com o FPS sem a latência do V-Sync.", EvidenceKind.Official,
            "Só funciona com monitor compatível e FreeSync ligado também no menu do monitor."),
    ];

    public static IReadOnlyList<string> GpuNotes { get; } =
    [
        "Escolha a fabricante da sua placa: o LZN Tweaker não identifica a placa de vídeo nesta seção e não aplica nada no driver.",
        "Mude uma opção por vez e teste algumas partidas antes da próxima.",
    ];
}
