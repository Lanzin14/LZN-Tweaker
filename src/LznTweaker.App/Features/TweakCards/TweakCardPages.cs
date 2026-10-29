using LznTweaker.Core.Maintenance;
using LznTweaker.Core.Tweaks;
using LznTweaker.Shell;

namespace LznTweaker.Features.TweakCards;

public sealed record CardTab(string Key, string Title, string Glyph, IReadOnlyList<string> TweakIds, IReadOnlyList<QuickActionDefinition> Actions);

public sealed record TweakCardsPage(string Title, string Description, IReadOnlyList<CardTab> Tabs);

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

    public static TweakCardsPage General { get; } = new("General", "Ajustes gerais do Windows: desempenho, privacidade, conveniência, apps e energia.",
    [
        new("core", "Core", "",
            [TweakCatalog.EnableGameModeId, TweakCatalog.DisableGameDvrId, TweakCatalog.DisableFullscreenOptId,
             TweakCatalog.DisableBackgroundAppsId, TweakCatalog.DisableStorageSenseId],
            [new("open-cleanup", "", "Limpar arquivos temporários",
                "Abre a Limpeza segura, que mostra cada local e o espaço estimado antes de apagar qualquer coisa.",
                RiskLevel.Low, "Abrir Limpeza", NavigateTo: ShellPages.Cleanup)]),
        new("privacy", "Privacy", "",
            [TweakCatalog.DisableTelemetryId, TweakCatalog.DisableConsumerFeaturesId, TweakCatalog.DisableActivityHistoryId,
             TweakCatalog.DisableLocationTrackingId, TweakCatalog.DisableRecallId, TweakCatalog.DisableCopilotId, TweakCatalog.DisableBingSearchId],
            []),
        new("qol", "QoL", "",
            [TweakCatalog.DisableStartupDelayId, TweakCatalog.DisableTransparencyId, TweakCatalog.EndTaskOnTaskbarId, TweakCatalog.DarkThemeId,
             TweakCatalog.ShowFileExtensionsId, TweakCatalog.ShowHiddenFilesId, TweakCatalog.NumLockOnStartupId, TweakCatalog.VerboseLogonMessagesId,
             TweakCatalog.DisableSnapWindowId, TweakCatalog.DisableSnapAssistId, TweakCatalog.DisableMouseAccelerationId, TweakCatalog.DisableStickyKeysId,
             TweakCatalog.DisableSearchButtonId, TweakCatalog.DisableTaskViewId, TweakCatalog.DisableTaskbarWidgetsId, TweakCatalog.TaskbarAlignLeftId],
            []),
        new("apps", "Apps", "",
            [TweakCatalog.AdobeNetworkBlockId, TweakCatalog.AdobeDebloatId],
            [new("open-debloat", "", "Apps pré-instalados",
                "Lista os apps da Store por categoria e mostra o que será removido antes de executar. Componentes essenciais ficam protegidos.",
                RiskLevel.Low, "Abrir Debloat", NavigateTo: ShellPages.Debloat),
             new("open-startup", "", "Apps na inicialização",
                "Revise os programas que abrem com o Windows; é possível reativar cada um depois.",
                RiskLevel.Low, "Abrir Inicialização", NavigateTo: ShellPages.Startup)]),
        new("power", "Power Plan", "",
            [TweakCatalog.ActivateUltimatePerformanceId, TweakCatalog.RemoveUltimatePerformanceId, TweakCatalog.DisableHibernationId],
            [new("power-options", "", "Opções de Energia do Windows",
                "Abre o painel nativo para conferir o plano ativo e a função dos botões (Inicialização Rápida).",
                RiskLevel.Low, "Abrir", Tool: SystemTool.OpenPowerOptions)])
    ]);

    public static TweakCardsPage Network { get; } = new("Network", "Diagnóstico, reparo e ajustes de rede documentados, com os efeitos explicados.",
    [
        new("diagnostics", "Diagnóstico", "", [],
            [new("connectivity", "", "Teste de conectividade",
                "Mede a latência até 1.1.1.1 e 8.8.8.8 e testa a resolução de DNS. Só leitura.",
                RiskLevel.Low, "Testar", Tool: SystemTool.ConnectivityTest),
             new("ipconfig", "", "Ver configuração de rede",
                "Abre o ipconfig /all em um console: adaptadores, IPs, gateway e servidores DNS.",
                RiskLevel.Low, "Abrir", Tool: SystemTool.ShowNetworkConfiguration),
             new("flushdns", "", "Limpar cache DNS",
                "Executa ipconfig /flushdns. Útil quando um site mudou de endereço ou há erros de resolução; não melhora o ping.",
                RiskLevel.Low, "Limpar", Tool: SystemTool.FlushDns)]),
        new("tweaks", "Ajustes", "",
            [TweakCatalog.SetIpv4PriorityId, TweakCatalog.DisableIpv6Id, TweakCatalog.DisableTeredoId], []),
        new("repair", "Reparo", "", [],
            [new("winsock", "", "Redefinir Winsock",
                "Executa netsh winsock reset para corrigir problemas de rede causados por software que alterou a pilha de rede (VPNs, antivírus antigos).",
                RiskLevel.Warning, "Redefinir", Tool: SystemTool.ResetWinsock,
                Confirmation: "O catálogo Winsock será redefinido para o padrão. Alguns programas de VPN ou filtros de rede podem precisar ser reinstalados, e o PC precisa ser reiniciado para concluir.\n\nContinuar?",
                Note: "Requer reinicialização")])
    ]);

    public static TweakCardsPage Fixes { get; } = new("Fixes", "Verificações de integridade e atalhos para as ferramentas nativas de reparo do Windows.",
    [
        new("integrity", "Integridade", "", [],
            [new("sfc", "", "Verificar arquivos do sistema (SFC)",
                "Executa sfc /scannow em um console visível. Corrige arquivos protegidos do Windows quando encontra divergências.",
                RiskLevel.Low, "Executar", Tool: SystemTool.SystemFileCheck,
                Confirmation: "O SFC pode levar de 10 a 30 minutos. Não feche o console até terminar.\n\nAbrir agora?"),
             new("dism-scan", "", "Verificar imagem do Windows (DISM)",
                "DISM /ScanHealth: verifica se a imagem do Windows está corrompida, sem alterar nada.",
                RiskLevel.Low, "Verificar", Tool: SystemTool.DismScanHealth),
             new("dism-restore", "", "Reparar imagem do Windows (DISM)",
                "DISM /RestoreHealth: repara a imagem baixando arquivos do Windows Update. Rode antes do SFC quando o SFC não consegue corrigir.",
                RiskLevel.Low, "Reparar", Tool: SystemTool.DismRestoreHealth,
                Confirmation: "O DISM usa o Windows Update e pode levar bastante tempo. Não desligue o PC durante o reparo.\n\nAbrir agora?"),
             new("chkdsk", "", "Verificar disco do sistema",
                "CHKDSK /scan verifica o sistema de arquivos com o Windows em uso, sem agendar reinício.",
                RiskLevel.Low, "Verificar", Tool: SystemTool.CheckDiskScan)]),
        new("tools", "Ferramentas", "", [],
            [new("troubleshoot", "", "Solução de problemas do Windows",
                "Abre os solucionadores oficiais (áudio, rede, Windows Update, impressora).",
                RiskLevel.Low, "Abrir", Tool: SystemTool.OpenTroubleshooters),
             new("rstrui", "", "Restauração do Sistema",
                "Abre o assistente nativo para voltar a um ponto de restauração, inclusive os criados pelo LznTweaker.",
                RiskLevel.Warning, "Abrir", Tool: SystemTool.OpenSystemRestore,
                Note: "Restaurar um ponto desfaz mudanças de sistema feitas depois dele.")])
    ]);
}
