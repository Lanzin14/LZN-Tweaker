using System.IO;
using System.Text.Json;
using LznTweaker.Core.Execution;

namespace LznTweaker.Core.GameOptimization;

public enum OptimizationArea { Windows, Steam, Cs2, Valorant, Overwatch2, Deadlock, Fortnite, Hardware }

public enum DiagnosticStatus
{
    /// <summary>Estado de acordo com a recomendação.</summary>
    Ok,
    /// <summary>Vale revisar; a recomendação explica a condição.</summary>
    Attention,
    /// <summary>Informativo: depende do uso ou do hardware.</summary>
    Info,
    /// <summary>Não foi possível ler.</summary>
    Unknown
}

/// <param name="Evidence">Oficial, Consenso, Autor do vídeo ou Sem evidência (ver docs/OTIMIZACAO-POR-JOGO.md).</param>
public sealed record DiagnosticItem(
    string Id,
    OptimizationArea Area,
    string Title,
    DiagnosticStatus Status,
    string CurrentState,
    string Recommendation,
    string Evidence,
    string Risk,
    string GamesAffected,
    string? RelatedFeature = null);

internal sealed record DiagnosticFacts(
    int? GameModeAuto, int? GameDvrEnabled, int? AppCaptureEnabled, int? HwSchMode, int? HiberbootEnabled, int? HibernateEnabled,
    int? OverlayTestMode, string? DirectXUserGlobalSettings, string? ActivePowerScheme, bool BcdReadable,
    Dictionary<string, string>? BcdTimer, int? TrimDisabled, List<VolumeFact>? Volumes, List<MemoryFact>? Memory, int? MemorySlots,
    string? BiosVersion, string? BiosDate, string? Board, List<string>? Gpus, string? CpuName, bool IsLaptop, List<ExecutableFact>? Executables);

internal sealed record VolumeFact(string Letter, long SizeBytes, long FreeBytes, string? MediaType, string? BusType);
internal sealed record MemoryFact(string? DeviceLocator, string? BankLabel, long CapacityBytes, int Speed, int ConfiguredClockSpeed, string? Manufacturer, string? PartNumber);
internal sealed record ExecutableFact(string Path, string? GpuPreference, string? LayersUser, string? LayersMachine);

/// <summary>Diagnóstico somente leitura de Windows, discos, hardware e executáveis dos jogos detectados.</summary>
public sealed class GamingDiagnosticsService(ScriptRunnerService scriptRunner)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<DiagnosticItem>> RunAsync(IReadOnlyList<DetectedGame> games, int? enabledStartupItems, CancellationToken cancellationToken = default)
    {
        var installed = games.Where(game => game.IsInstalled).ToArray();
        // A Steam grava caminhos em minúsculas ("c:\program files…"); a letra é normalizada para exibição.
        var letters = installed.Select(game => Path.GetPathRoot(game.InstallPath!)?.TrimEnd('\\', ':'))
            .Append(Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\', ':'))
            .Where(letter => letter is { Length: 1 }).Select(letter => letter!.ToUpperInvariant()).Distinct().ToArray();
        var payload = JsonSerializer.Serialize(new
        {
            DriveLetters = letters,
            Executables = installed.Where(game => game.ExecutablePath is not null).Select(game => game.ExecutablePath).ToArray()
        });
        var run = await scriptRunner.RunAsync(
            new ScriptRunRequest("diagnostics", EmbeddedScripts.GamingDiagnostics, payload, TimeSpan.FromMinutes(2), RequiresElevation: true),
            cancellationToken: cancellationToken);
        if (run.ResultJson is null)
            throw new InvalidOperationException($"O diagnóstico não pôde ser executado: {run.ErrorOutput ?? run.Message}");
        var facts = JsonSerializer.Deserialize<DiagnosticFacts>(run.ResultJson, JsonOptions)
                    ?? throw new InvalidOperationException("O resultado do diagnóstico não pôde ser interpretado.");
        return DiagnosticRules.Evaluate(facts, games, enabledStartupItems);
    }
}

/// <summary>Regras puras: transformam fatos lidos do sistema em itens com estado, recomendação condicional e evidência.</summary>
internal static class DiagnosticRules
{
    private const string AllGames = "Todos os jogos";

    public static IReadOnlyList<DiagnosticItem> Evaluate(DiagnosticFacts facts, IReadOnlyList<DetectedGame> games, int? enabledStartupItems)
    {
        var items = new List<DiagnosticItem>();
        var windows = OptimizationArea.Windows;

        // A11: Modo de Jogo (ausente = padrão ligado)
        var gameMode = facts.GameModeAuto ?? 1;
        items.Add(new("windows-game-mode", windows, "Modo de Jogo", gameMode == 1 ? DiagnosticStatus.Ok : DiagnosticStatus.Attention,
            gameMode == 1 ? "Ativado" : "Desativado (AutoGameModeEnabled = 0)",
            "O autor do vídeo recomenda manter ligado: ajuda o Windows a priorizar o jogo e é o padrão. Para reativar: Configurações > Jogos > Modo de Jogo.",
            "Autor do vídeo; padrão do Windows", "Baixo", AllGames));

        // A10: capturas da Game Bar
        var capturesOff = facts.GameDvrEnabled == 0 && facts.AppCaptureEnabled != 1;
        items.Add(new("windows-game-captures", windows, "Capturas em segundo plano da Game Bar", capturesOff ? DiagnosticStatus.Ok : DiagnosticStatus.Attention,
            capturesOff ? "Desativadas" : "Gravação/captura em segundo plano ativa ou não configurada",
            "Desative as capturas (a Game Bar em si pode continuar ligada). Use o tweak \"Desativar Game DVR\" na página Tweaks.",
            "Oficial", "Baixo", AllGames, "Tweaks › Desativar Game DVR"));

        // A12: HAGS
        items.Add(new("windows-hags", windows, "Agendamento de GPU acelerado por hardware (HAGS)",
            facts.HwSchMode is null ? DiagnosticStatus.Unknown : DiagnosticStatus.Info,
            facts.HwSchMode switch { 2 => "Ativado", 1 => "Desativado", _ => "Não informado pelo driver" },
            "Depende do uso: o autor prefere desligado, mas a geração de quadros (DLSS Frame Generation e similares) exige HAGS ligado. " +
            "Mude em Configurações > Sistema > Tela > Elementos gráficos e compare no seu jogo. Requer reinicialização.",
            "Autor do vídeo (desligar); Oficial (requisito da geração de quadros)", "Médio", AllGames));

        // A9: Inicialização Rápida
        var fastStartup = facts.HiberbootEnabled == 1 && facts.HibernateEnabled != 0;
        items.Add(new("windows-fast-startup", windows, "Inicialização Rápida", fastStartup ? DiagnosticStatus.Attention : DiagnosticStatus.Ok,
            fastStartup ? "Ativada" : "Desativada",
            "Com ela ativa, \"Desligar\" hiberna o kernel e o sistema não reinicia do zero. Desative em Opções de Energia > Escolher a função dos botões, " +
            "ou use o tweak \"Desativar Hibernação\", que também a desliga.",
            "Consenso", "Baixo", AllGames, "Tweaks › Desativar Hibernação"));

        // A8: plano de energia
        items.Add(PowerPlan(facts));

        // A6: MPO
        var mpoDisabled = facts.OverlayTestMode == 5;
        items.Add(new("windows-mpo", windows, "Multiplane Overlay (MPO)", mpoDisabled ? DiagnosticStatus.Attention : DiagnosticStatus.Ok,
            mpoDisabled ? "Desativado por HKLM\\SOFTWARE\\Microsoft\\Windows\\Dwm\\OverlayTestMode = 5" : "Sem sobreposição (padrão do Windows)",
            mpoDisabled
                ? "Essa chave é o paliativo documentado para cintilação/tela preta em alguns drivers. Se você não a criou por esse motivo, o vídeo recomenda " +
                  "apagá-la (regedit > HKLM\\SOFTWARE\\Microsoft\\Windows\\Dwm > excluir OverlayTestMode) e reiniciar, para o jogo poder usar independent flip. " +
                  "O app não remove automaticamente porque quem a criou pode ter feito isso para corrigir um defeito."
                : "Nada a fazer.",
            "Oficial (chave de diagnóstico da Microsoft/NVIDIA)", "Médio", AllGames));

        // A14: otimizações em janela e VRR do Windows
        var dx = facts.DirectXUserGlobalSettings ?? string.Empty;
        items.Add(new("windows-windowed-vrr", windows, "Otimizações para jogos em janela e VRR do Windows", DiagnosticStatus.Info,
            $"Otimizações em janela: {ReadToggle(dx, "SwapEffectUpgradeEnable")} · Taxa de atualização variável: {ReadToggle(dx, "VRROptimizeEnable")}",
            "O vídeo sugere otimizações em janela ligadas e a opção de taxa de atualização variável desligada. Não há documentação de conflito; " +
            "a opção de VRR do Windows só age em jogos sem suporte nativo a VRR. Ajuste em Configurações > Sistema > Tela > Elementos gráficos > Configurações padrão.",
            "Autor do vídeo", "Baixo", AllGames));

        // A7: BCD
        items.Add(Bcd(facts));

        // A3–A5: discos
        items.AddRange(Volumes(facts, games));

        // A18: inicialização
        if (enabledStartupItems is { } startup)
            items.Add(new("windows-startup", windows, "Programas na inicialização", startup > 10 ? DiagnosticStatus.Attention : DiagnosticStatus.Info,
                $"{startup} item(ns) ativado(s)",
                "Revise a página Inicialização. Programas de RGB e de monitoramento de hardware podem gerar interrupções e uso de CPU durante o jogo.",
                "Consenso", "Baixo", AllGames, "Inicialização"));

        items.AddRange(GameExecutables(facts, games));
        items.AddRange(Hardware(facts));
        return items;
    }

    private static DiagnosticItem PowerPlan(DiagnosticFacts facts)
    {
        var scheme = facts.ActivePowerScheme ?? string.Empty;
        var (status, state) = scheme.ToLowerInvariant() switch
        {
            var s when s.Contains("381b4222-f694-41f0-9685-ff5bb260df2e") => (DiagnosticStatus.Attention, "Equilibrado"),
            var s when s.Contains("a1841308-3541-4fab-bc81-f71556f20b4a") => (DiagnosticStatus.Attention, "Economia de energia"),
            var s when s.Contains("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c") => (DiagnosticStatus.Ok, "Alto desempenho"),
            "" => (DiagnosticStatus.Unknown, "Não lido"),
            _ => (DiagnosticStatus.Info, "Plano personalizado ou Ultimate Performance")
        };
        return new("windows-power-plan", OptimizationArea.Windows, "Plano de energia", status, state,
            (facts.IsLaptop ? "Notebook detectado: planos de alto desempenho aumentam consumo e temperatura; avalie na tomada. " : string.Empty) +
            "O autor evita o Equilibrado e usa Alto desempenho ou Desempenho Máximo (a diferença entre os dois é pequena). AMD e Intel indicam o Equilibrado " +
            "com o driver de chipset atualizado em vários processadores, então compare no seu jogo.",
            "Autor do vídeo; recomendações de fabricantes divergem", "Baixo", AllGames, "Tweaks › Performance Plans");
    }

    private static DiagnosticItem Bcd(DiagnosticFacts facts)
    {
        if (!facts.BcdReadable)
            return new("windows-bcd-timer", OptimizationArea.Windows, "Temporizador do sistema (BCD)", DiagnosticStatus.Unknown,
                "Não foi possível ler o BCD (exige administrador)",
                "Abra o LznTweaker como administrador para verificar useplatformclock, useplatformtick e disabledynamictick.",
                "Oficial", "Alto", AllGames);

        var overrides = facts.BcdTimer ?? [];
        return new("windows-bcd-timer", OptimizationArea.Windows, "Temporizador do sistema (BCD)",
            overrides.Count == 0 ? DiagnosticStatus.Ok : DiagnosticStatus.Attention,
            overrides.Count == 0 ? "Nenhuma sobreposição (padrão do Windows)" : string.Join(" · ", overrides.Select(pair => $"{pair.Key} = {pair.Value}")),
            overrides.Count == 0
                ? "Nada a fazer: o Windows escolhe o temporizador (normalmente o TSC)."
                : "O vídeo recomenda voltar ao padrão. Em um PowerShell como administrador, rode para cada item: " +
                  string.Join(" ; ", overrides.Keys.Select(key => $"bcdedit /deletevalue {key}")) +
                  " e reinicie. Observação do vídeo: useplatformclock = Yes força o HPET (o contrário do que muitos guias dizem). O app não altera o BCD automaticamente.",
            "Oficial (opções do bcdedit); recomendação do autor", "Alto", AllGames);
    }

    private static IEnumerable<DiagnosticItem> Volumes(DiagnosticFacts facts, IReadOnlyList<DetectedGame> games)
    {
        foreach (var volume in facts.Volumes ?? [])
        {
            var onVolume = games.Where(game => game.IsInstalled && string.Equals(Path.GetPathRoot(game.InstallPath!)?.TrimEnd('\\', ':'), volume.Letter, StringComparison.OrdinalIgnoreCase))
                .Select(game => game.Game.Name).ToArray();
            var freePercent = volume.SizeBytes <= 0 ? 0 : volume.FreeBytes * 100d / volume.SizeBytes;
            var isSsd = string.Equals(volume.MediaType, "SSD", StringComparison.OrdinalIgnoreCase);
            var affected = onVolume.Length == 0 ? "Windows" : string.Join(", ", onVolume);
            yield return new($"disk-{volume.Letter}-free", OptimizationArea.Windows, $"Espaço livre em {volume.Letter}:",
                freePercent < 15 ? DiagnosticStatus.Attention : DiagnosticStatus.Ok,
                $"{freePercent:0}% livre ({ShaderCacheService.FormatBytes(volume.FreeBytes)} de {ShaderCacheService.FormatBytes(volume.SizeBytes)})",
                freePercent < 15
                    ? "Abaixo dos 15–20% citados no vídeo. Em SSD, pouco espaço livre reduz o cache rápido (SLC) e a margem para a coleta de lixo, " +
                      "o que pode causar lentidão em gravações longas. Libere espaço (a página Limpeza ajuda)."
                    : "Espaço livre suficiente.",
                "Consenso (o motivo técnico dado no vídeo é impreciso)", "Baixo", affected, "Limpeza");
            yield return new($"disk-{volume.Letter}-media", OptimizationArea.Windows, $"Tipo de unidade em {volume.Letter}:",
                volume.MediaType is null ? DiagnosticStatus.Unknown : isSsd ? DiagnosticStatus.Ok : DiagnosticStatus.Attention,
                volume.MediaType is null ? "Não informado" : $"{volume.MediaType}{(volume.BusType is null ? string.Empty : $" ({volume.BusType})")}",
                isSsd ? "Jogos em SSD, como o vídeo recomenda." : "Prefira instalar os jogos em um SSD para carregamentos e streaming de texturas mais rápidos.",
                "Consenso", "—", affected);
        }

        yield return new("disk-trim", OptimizationArea.Windows, "TRIM do SSD",
            facts.TrimDisabled switch { 0 => DiagnosticStatus.Ok, 1 => DiagnosticStatus.Attention, _ => DiagnosticStatus.Unknown },
            facts.TrimDisabled switch { 0 => "Ativo", 1 => "Desativado", _ => "Não lido" },
            facts.TrimDisabled == 1
                ? "Reative com: fsutil behavior set DisableDeleteNotify 0 (como administrador)."
                : "O Windows já executa o ReTrim semanalmente. \"Otimizar unidades\" faz o ReTrim na hora, se quiser (não é desfragmentação em SSD).",
            "Oficial", "Baixo", AllGames);
    }

    private static IEnumerable<DiagnosticItem> GameExecutables(DiagnosticFacts facts, IReadOnlyList<DetectedGame> games)
    {
        foreach (var game in games.Where(game => game.ExecutablePath is not null))
        {
            var area = AreaOf(game.Game);
            var fact = facts.Executables?.FirstOrDefault(exe => string.Equals(exe.Path, game.ExecutablePath, StringComparison.OrdinalIgnoreCase));
            // O valor é uma lista "Chave=valor;" (ex.: "GpuPreference=2;" ou só "AppStatus=0;"). Só GpuPreference importa aqui.
            var preference = ReadSetting(fact?.GpuPreference ?? string.Empty, "GpuPreference");
            var highPerformance = preference == "2";
            yield return new($"{game.Game.Id}-gpu-preference", area, "Preferência de GPU do executável",
                highPerformance ? DiagnosticStatus.Ok : DiagnosticStatus.Info,
                preference switch
                {
                    "2" => "Alto desempenho",
                    "1" => "Economia de energia",
                    "0" => "Deixar o Windows decidir",
                    _ => "Padrão do Windows (não definido)"
                },
                highPerformance
                    ? "Já está em Alto desempenho, como o vídeo recomenda."
                    : $"Em Configurações > Sistema > Tela > Elementos gráficos, adicione {Path.GetFileName(game.ExecutablePath)} e escolha Alto desempenho. " +
                      "Faz diferença principalmente em PCs com GPU integrada e dedicada.",
                "Oficial", "Baixo", game.Game.Name);

            var layers = $"{fact?.LayersUser} {fact?.LayersMachine}";
            var fsoDisabled = layers.Contains("DISABLEDXMAXIMIZEDWINDOWEDMODE", StringComparison.OrdinalIgnoreCase);
            yield return new($"{game.Game.Id}-fullscreen-optimizations", area, "Otimizações de tela inteira (compatibilidade)",
                fsoDisabled ? DiagnosticStatus.Attention : DiagnosticStatus.Ok,
                fsoDisabled ? "\"Desabilitar otimizações de tela inteira\" está marcado" : "Não desabilitadas (padrão)",
                fsoDisabled
                    ? $"O vídeo recomenda deixar desmarcado: clique com o botão direito em {Path.GetFileName(game.ExecutablePath)} > Propriedades > Compatibilidade."
                    : "Nada a fazer.",
                "Oficial (AppCompatFlags\\Layers); recomendação do autor", "Baixo", game.Game.Name);
        }
    }

    private static IEnumerable<DiagnosticItem> Hardware(DiagnosticFacts facts)
    {
        var hardware = OptimizationArea.Hardware;
        var memory = facts.Memory ?? [];
        if (memory.Count > 0)
        {
            var total = memory.Sum(module => module.CapacityBytes);
            var channels = memory.Select(module => module.BankLabel ?? module.DeviceLocator ?? string.Empty).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var dual = memory.Count >= 2 && channels >= 2;
            yield return new("hw-ram-channels", hardware, "Memória RAM em dual channel",
                memory.Count == 1 ? DiagnosticStatus.Attention : dual ? DiagnosticStatus.Ok : DiagnosticStatus.Info,
                $"{memory.Count} módulo(s), {ShaderCacheService.FormatBytes(total)} · " + string.Join(", ", memory.Select(module => $"{module.DeviceLocator} ({module.BankLabel})")),
                memory.Count == 1
                    ? "Um único módulo trabalha em single channel. Dois módulos iguais em dual channel costumam render bem mais em jogos limitados pela CPU."
                    : "Confirme no CPU-Z (aba Memory, \"Channel\") e no manual da placa-mãe os slots recomendados (na maioria das placas de 4 slots, A2/B2 — o 2º e o 4º).",
                "Consenso", "—", AllGames);

            var configured = memory.Max(module => module.ConfiguredClockSpeed);
            var rated = memory.Max(module => module.Speed);
            var xmpLikely = configured > rated && rated > 0;
            yield return new("hw-ram-xmp", hardware, "Frequência da RAM (XMP/EXPO)",
                configured <= 0 ? DiagnosticStatus.Unknown : xmpLikely ? DiagnosticStatus.Ok : DiagnosticStatus.Info,
                $"Configurada: {configured} MT/s · SPD padrão (JEDEC): {rated} MT/s",
                xmpLikely
                    ? "Acima do padrão JEDEC: o perfil XMP/EXPO/DOCP parece ativo."
                    : "A memória está no padrão JEDEC. Se os módulos são vendidos com frequência maior, ative o XMP/EXPO na BIOS (o app nunca altera a BIOS). " +
                      "Em Ryzen, confira no ZenTimings se MCLK e UCLK estão iguais (1:1); em DDR5 acima de ~6000–6200 isso pode deixar de acontecer.",
                "Consenso", "Médio (BIOS)", AllGames);
        }

        yield return new("hw-bios", hardware, "Versão do BIOS", facts.BiosVersion is null ? DiagnosticStatus.Unknown : DiagnosticStatus.Info,
            facts.BiosVersion is null ? "Não lida" : $"{facts.BiosVersion} ({facts.BiosDate}) · {facts.Board}",
            "Compare com a versão mais recente no site oficial do fabricante da placa-mãe. Atualizações de BIOS podem melhorar CPU e memória, " +
            "mas exigem cuidado (não desligue o PC durante a atualização). O app não atualiza BIOS.",
            "Consenso", "Alto (atualização de firmware)", AllGames);

        var gpus = facts.Gpus ?? [];
        var cpu = facts.CpuName?.Trim() ?? string.Empty;
        var amdGpu = gpus.Any(gpu => gpu.Contains("AMD", StringComparison.OrdinalIgnoreCase) || gpu.Contains("Radeon", StringComparison.OrdinalIgnoreCase));
        yield return new("hw-chipset-driver", hardware, "Driver de chipset", DiagnosticStatus.Info,
            cpu.Length == 0 ? "Processador não identificado" : cpu,
            cpu.Contains("AMD", StringComparison.OrdinalIgnoreCase)
                ? "Baixe o driver de chipset mais recente em amd.com/support pelo chipset da placa (ex.: B550), não pelo modelo da placa."
                : "Baixe o Intel Chipset Software / Driver & Support Assistant em intel.com.",
            "Oficial (fabricantes)", "Baixo", AllGames);

        yield return new("hw-rebar", hardware, "Resizable BAR / Smart Access Memory", DiagnosticStatus.Info,
            gpus.Count == 0 ? "GPU não identificada" : string.Join(" · ", gpus),
            "Verifique no GPU-Z se está ativo. Ajuda em jogos limitados pela GPU; pode piorar jogos limitados pela CPU. " +
            (amdGpu
                ? "Com GPU AMD, o SAM vale para todos os jogos; o autor sugere testar desligado no CS2."
                : "A NVIDIA só ativa o ReBAR em jogos validados por ela.") +
            " Muda na BIOS: o app não altera.",
            "Consenso; sugestão do CS2 é do autor (\"teria que testar\")", "Médio (BIOS)", "Todos os jogos; atenção ao CS2");
    }

    private static OptimizationArea AreaOf(SupportedGame game) => game.Id switch
    {
        SupportedGames.Cs2Id => OptimizationArea.Cs2,
        SupportedGames.ValorantId => OptimizationArea.Valorant,
        SupportedGames.Overwatch2Id => OptimizationArea.Overwatch2,
        SupportedGames.FortniteId => OptimizationArea.Fortnite,
        _ => OptimizationArea.Deadlock
    };

    private static string ReadToggle(string settings, string key) => ReadSetting(settings, key) switch
    {
        "1" => "ligado",
        null => "padrão do Windows",
        _ => "desligado"
    };

    private static string? ReadSetting(string settings, string key)
    {
        foreach (var part in settings.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && pair[0].Equals(key, StringComparison.OrdinalIgnoreCase))
                return pair[1];
        }
        return null;
    }
}
