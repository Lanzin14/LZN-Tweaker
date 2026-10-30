using LznTweaker.Core.Guides;
using LznTweaker.Core.Maintenance;
using LznTweaker.Core.Tweaks;
using LznTweaker.Shell;

namespace LznTweaker.Features.TweakCards;

/// <param name="Description">Frase curta exibida sob as abas quando esta aba está ativa.</param>
/// <param name="Guides">Configurações manuais de outros aplicativos (o app só mostra o passo a passo).</param>
/// <param name="Highlight">Aviso em destaque exibido acima dos cards quando esta aba está ativa.</param>
/// <param name="Categories">Grupos de cards exibidos juntos nesta aba, cada um com o seu cabeçalho.</param>
public sealed record CardTab(string Key, string Title, string Glyph, IReadOnlyList<string> TweakIds, IReadOnlyList<QuickActionDefinition> Actions, string Description = "",
    IReadOnlyList<ManualSettingGuide>? Guides = null, string? Highlight = null, IReadOnlyList<CardCategory>? Categories = null);

/// <summary>Grupo de cards dentro de uma aba: os ajustes aparecem juntos, sob um cabeçalho com nome, ícone e descrição.</summary>
public sealed record CardCategory(string Key, string Title, string Glyph, IReadOnlyList<string> TweakIds, IReadOnlyList<QuickActionDefinition> Actions, string Description = "");

public sealed record TweakCardsPage(string Title, CategoryGuide Guide, IReadOnlyList<CardTab> Tabs);

/// <summary>
/// Organização das páginas de cards. Cada id aparece em uma única aba. O ponto de restauração (automático em todo lote)
/// e a limpeza de temporários (exige prévia, na página Limpeza) não viram cards.
/// </summary>
public static class TweakCardPages
{
    private static readonly Dictionary<string, string> Glyphs = new(StringComparer.Ordinal)
    {
        [TweakCatalog.DisableGameDvrId] = "",
        [TweakCatalog.EnableGameModeId] = "",
        [TweakCatalog.DisableFullscreenOptId] = "",
        [TweakCatalog.DisableBackgroundAppsId] = "",
        [TweakCatalog.DisableStorageSenseId] = "",
        [TweakCatalog.DisableTelemetryId] = "",
        [TweakCatalog.DisableLocationTrackingId] = "",
        [TweakCatalog.DisableActivityHistoryId] = "",
        [TweakCatalog.DisableBingSearchId] = "",
        [TweakCatalog.DarkThemeId] = "",
        [TweakCatalog.DisableTransparencyId] = "",
        [TweakCatalog.DisableMouseAccelerationId] = "",
        [TweakCatalog.DisableStickyKeysId] = "",
        [TweakCatalog.NumLockOnStartupId] = "",
        [TweakCatalog.ShowFileExtensionsId] = "",
        [TweakCatalog.ShowHiddenFilesId] = "",
        [TweakCatalog.ActivateUltimatePerformanceId] = "",
        [TweakCatalog.RemoveUltimatePerformanceId] = "",
        [TweakCatalog.DisableHibernationId] = "",
        [TweakCatalog.SetIpv4PriorityId] = "",
        [TweakCatalog.DisableIpv6Id] = "",
        [TweakCatalog.DisableTeredoId] = ""
    };

    public static string GlyphFor(string tweakId, string fallback) => Glyphs.GetValueOrDefault(tweakId, fallback);

    public static TweakCardsPage General { get; } = new("Geral", CategoryGuides.General,
    [
        new("windows", "Ajustes do Windows", "\uE713", [], [],
            Description: "Todos os ajustes do Windows em uma lista, separados por categoria. A busca e os filtros valem para todos.",
            Categories:
            [
                new("core", "Desempenho em jogos", "",
                    [TweakCatalog.EnableGameModeId, TweakCatalog.DisableGameDvrId, TweakCatalog.DisableFullscreenOptId,
                     TweakCatalog.DisableBackgroundAppsId, TweakCatalog.DisableStorageSenseId],
                    [new("open-cleanup", "", "Limpar arquivos temporários",
                        "Leva à página Limpeza, que mostra cada local e o espaço estimado antes de apagar.",
                        RiskLevel.Low, "Abrir Limpeza", NavigateTo: ShellPages.Cleanup)],
                    Description: "Ajustes ligados diretamente aos jogos: Modo de Jogo, gravação em segundo plano, tela cheia, apps em segundo plano e Storage Sense."),
                new("privacy", "Privacidade", "",
                    [TweakCatalog.DisableTelemetryId, TweakCatalog.DisableConsumerFeaturesId, TweakCatalog.DisableActivityHistoryId,
                     TweakCatalog.DisableLocationTrackingId, TweakCatalog.DisableRecallId, TweakCatalog.DisableCopilotId, TweakCatalog.DisableBingSearchId],
                    [],
                    Description: "Reduz os dados de uso enviados à Microsoft e desliga recursos como Recall, Copilot e a busca do Bing no Iniciar."),
                new("qol", "Qualidade de vida", "",
                    [TweakCatalog.DisableStartupDelayId, TweakCatalog.DisableTransparencyId, TweakCatalog.EndTaskOnTaskbarId, TweakCatalog.DarkThemeId,
                     TweakCatalog.ShowFileExtensionsId, TweakCatalog.ShowHiddenFilesId, TweakCatalog.NumLockOnStartupId, TweakCatalog.VerboseLogonMessagesId,
                     TweakCatalog.DisableSnapWindowId, TweakCatalog.DisableSnapAssistId, TweakCatalog.DisableMouseAccelerationId, TweakCatalog.DisableStickyKeysId,
                     TweakCatalog.DisableSearchButtonId, TweakCatalog.DisableTaskViewId, TweakCatalog.DisableTaskbarWidgetsId, TweakCatalog.TaskbarAlignLeftId],
                    [],
                    Description: "Conforto no dia a dia: aparência, barra de tarefas, Explorador de Arquivos, teclado e mouse. Em geral, não muda o FPS."),
                new("apps", "Apps", "",
                    [TweakCatalog.AdobeNetworkBlockId, TweakCatalog.AdobeDebloatId],
                    [new("open-debloat", "", "Apps pré-instalados",
                        "Leva à página Debloat, que lista os apps instalados e mostra o que será removido antes de executar.",
                        RiskLevel.Low, "Abrir Debloat", NavigateTo: ShellPages.Debloat),
                     new("open-startup", "", "Apps na inicialização",
                        "Leva à página Inicialização, onde você escolhe o que abre junto com o Windows.",
                        RiskLevel.Low, "Abrir Inicialização", NavigateTo: ShellPages.Startup)],
                    Description: "Bloqueio e limpeza de serviços da Adobe, e atalhos para remover apps pré-instalados e controlar o que abre com o Windows."),
                new("power", "Plano de energia", "",
                    [TweakCatalog.ActivateUltimatePerformanceId, TweakCatalog.RemoveUltimatePerformanceId, TweakCatalog.DisableHibernationId],
                    [new("power-options", "", "Opções de Energia do Windows",
                        "Abre o painel do Windows para conferir o plano de energia ativo e a Inicialização Rápida.",
                        RiskLevel.Low, "Abrir", Tool: SystemTool.OpenPowerOptions)],
                    Description: "Como o PC usa energia: o plano de energia define o ritmo do processador; a hibernação reserva espaço em disco.")
            ]),
        new("discord", "Discord", "", [], [],
            Description: "Ajustes feitos por você dentro do aplicativo do Discord. O LZN Tweaker não altera o Discord: cada card mostra o caminho e o passo a passo.",
            Guides: DiscordGuides.All,
            Highlight: "IMPORTANTE SE VOCÊ USA DISCORD.")
    ]);

    public static TweakCardsPage Network { get; } = new("Network", CategoryGuides.Network,
    [
        new("diagnostics", "Diagnóstico", "", [],
            [new("connectivity", "", "Teste de conectividade",
                "Mede o tempo de resposta até 1.1.1.1 (Cloudflare) e 8.8.8.8 (Google) e testa se os nomes de sites são encontrados. Só leitura.",
                RiskLevel.Low, "Testar", Tool: SystemTool.ConnectivityTest),
             new("ipconfig", "", "Ver configuração de rede",
                "Mostra em um console as placas de rede, os endereços IP, o roteador (gateway) e os servidores DNS (ipconfig /all).",
                RiskLevel.Low, "Abrir", Tool: SystemTool.ShowNetworkConfiguration),
             new("flushdns", "", "Limpar cache DNS",
                "Apaga a lista de endereços de sites guardada pelo Windows (ipconfig /flushdns). Ajuda quando um site não abre ou mudou de endereço; não melhora o ping.",
                RiskLevel.Low, "Limpar", Tool: SystemTool.FlushDns)],
            Description: "Testes que só leem a conexão; nada é alterado."),
        new("tweaks", "Ajustes", "",
            [TweakCatalog.SetIpv4PriorityId, TweakCatalog.DisableIpv6Id, TweakCatalog.DisableTeredoId], [],
            Description: "Preferência entre IPv4 e IPv6 e o túnel Teredo. Ajudam em redes com problemas de IPv6; não reduzem o ping."),
        new("repair", "Reparo", "", [],
            [new("winsock", "", "Redefinir Winsock",
                "Volta a configuração de rede do Windows ao padrão (netsh winsock reset). Corrige falhas deixadas por VPNs ou antivírus antigos.",
                RiskLevel.Warning, "Redefinir", Tool: SystemTool.ResetWinsock,
                Confirmation: "O catálogo Winsock será redefinido para o padrão. Alguns programas de VPN ou filtros de rede podem precisar ser reinstalados, e o PC precisa ser reiniciado para concluir.\n\nContinuar?",
                Note: "Requer reinicialização")],
            Description: "Para quando a internet para de funcionar depois de instalar ou remover VPN, antivírus ou outro programa de rede.")
    ]);

    public static TweakCardsPage Fixes { get; } = new("Fixes", CategoryGuides.Fixes,
    [
        new("integrity", "Integridade", "", [],
            [new("sfc", "", "Verificar arquivos do sistema (SFC)",
                "Procura arquivos do Windows danificados ou alterados e os substitui pela versão original (sfc /scannow).",
                RiskLevel.Low, "Executar", Tool: SystemTool.SystemFileCheck,
                Confirmation: "O SFC pode levar de 10 a 30 minutos. Não feche o console até terminar.\n\nAbrir agora?"),
             new("dism-scan", "", "Verificar imagem do Windows (DISM)",
                "Verifica se a imagem do Windows, a cópia que o SFC usa para reparar, está danificada (DISM /ScanHealth). Não altera nada.",
                RiskLevel.Low, "Verificar", Tool: SystemTool.DismScanHealth),
             new("dism-restore", "", "Reparar imagem do Windows (DISM)",
                "Repara a imagem do Windows baixando arquivos do Windows Update (DISM /RestoreHealth). Use quando o SFC não conseguir corrigir e rode o SFC de novo depois.",
                RiskLevel.Low, "Reparar", Tool: SystemTool.DismRestoreHealth,
                Confirmation: "O DISM usa o Windows Update e pode levar bastante tempo. Não desligue o PC durante o reparo.\n\nAbrir agora?"),
             new("chkdsk", "", "Verificar disco do sistema",
                "Procura erros na estrutura de arquivos do disco do Windows sem precisar reiniciar (CHKDSK /scan).",
                RiskLevel.Low, "Verificar", Tool: SystemTool.CheckDiskScan)],
            Description: "Verificam e reparam os arquivos do Windows e o disco. Abrem em uma janela visível e podem demorar."),
        new("tools", "Ferramentas", "", [],
            [new("troubleshoot", "", "Solução de problemas do Windows",
                "Abre os assistentes do Windows que diagnosticam problemas de áudio, rede, Windows Update e impressora.",
                RiskLevel.Low, "Abrir", Tool: SystemTool.OpenTroubleshooters),
             new("rstrui", "", "Restauração do Sistema",
                "Abre o assistente do Windows para voltar o sistema a um ponto de restauração, inclusive os criados pelo LZN Tweaker.",
                RiskLevel.Warning, "Abrir", Tool: SystemTool.OpenSystemRestore,
                Note: "Restaurar um ponto desfaz mudanças de sistema feitas depois dele.")],
            Description: "Atalhos para as ferramentas de reparo e recuperação do próprio Windows.")
    ]);
}
