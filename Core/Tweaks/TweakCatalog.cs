namespace LznTweaker.Core.Tweaks;

public enum TweakCategory
{
    Essential,
    Advanced,
    CustomizePreferences,
    PerformancePlans
}

public enum TweakPreset
{
    Minimal,
    Standard,
    Advanced,
    Custom
}

public enum TweakSystemState
{
    Unknown,
    Enabled,
    Disabled,
    Mixed,
    Unsupported,
    BlockedByPolicy
}

public sealed record RegistryValueChange(
    string SubKey,
    string Name,
    Microsoft.Win32.RegistryValueKind Kind,
    object Value);

public sealed record ServiceChange(
    string Name,
    string TargetStartupType);

public sealed record TweakDefinition(
    string Id,
    string ReferenceKey,
    string Name,
    string Description,
    string TechnicalReason,
    TweakCategory Category,
    string RiskLevel,
    string Compatibility,
    bool RequiresAdministrator,
    bool RequiresRestart,
    bool HasReliableRollback,
    bool IsOneShotOperation,
    string SourceUrl,
    IReadOnlyList<RegistryValueChange>? RegistryChanges = null,
    string? RestartNote = null,
    IReadOnlyList<RegistryValueChange>? HklmRegistryChanges = null,
    IReadOnlyList<ServiceChange>? ServiceChanges = null,
    IReadOnlyList<string>? ScheduledTasksToDisable = null,
    string? InvokeScript = null,
    string? UndoScript = null,
    string? VerifyScript = null);

public static class TweakCatalog
{
    // ═══════════ ID Constants — Essential ═══════════
    public const string CreateRestorePointId = "create-restore-point";
    public const string EndTaskOnTaskbarId = "end-task-on-taskbar";
    public const string DeleteTemporaryFilesId = "delete-temporary-files";
    public const string DisableTelemetryId = "disable-telemetry";
    public const string DisableConsumerFeaturesId = "disable-consumer-features";
    public const string DisableActivityHistoryId = "disable-activity-history";
    public const string DisableGameDvrId = "disable-game-dvr";
    public const string DisableHibernationId = "disable-hibernation";
    public const string SetIpv4PriorityId = "set-ipv4-priority";
    public const string DisableLocationTrackingId = "disable-location-tracking";
    public const string DisableStorageSenseId = "disable-storage-sense";
    public const string DisableRecallId = "disable-recall";

    // ═══════════ ID Constants — Customize Preferences ═══════════
    public const string DarkThemeId = "dark-theme-windows";
    public const string ShowFileExtensionsId = "show-file-extensions";
    public const string DisableBingSearchId = "disable-bing-search";
    public const string NumLockOnStartupId = "numlock-on-startup";
    public const string VerboseLogonMessagesId = "verbose-logon-messages";
    public const string DisableSnapWindowId = "disable-snap-window";
    public const string DisableSnapAssistId = "disable-snap-assist";
    public const string DisableMouseAccelerationId = "disable-mouse-acceleration";
    public const string DisableStickyKeysId = "disable-sticky-keys";
    public const string ShowHiddenFilesId = "show-hidden-files";
    public const string DisableSearchButtonId = "disable-search-button";
    public const string DisableTaskViewId = "disable-task-view";
    public const string DisableTaskbarWidgetsId = "disable-taskbar-widgets";
    public const string TaskbarAlignLeftId = "taskbar-align-left";

    // ═══════════ ID Constants — Advanced ═══════════
    public const string AdobeNetworkBlockId = "adobe-network-block";
    public const string AdobeDebloatId = "adobe-debloat";
    public const string DisableIpv6Id = "disable-ipv6";
    public const string DisableTeredoId = "disable-teredo";
    public const string DisableBackgroundAppsId = "disable-background-apps";
    public const string DisableFullscreenOptId = "disable-fullscreen-optimizations";
    public const string DisableCopilotId = "disable-copilot";

    // ═══════════ ID Constants — Performance Plans ═══════════
    public const string ActivateUltimatePerformanceId = "activate-ultimate-performance";
    public const string RemoveUltimatePerformanceId = "remove-ultimate-performance";

    // ═══════════ Common Registry Paths ═══════════
    private const string ExplorerAdvancedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string ThemePersonalizePath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string GameConfigStorePath = @"System\GameConfigStore";
    private const string WinUtilSourceUrl = "https://github.com/ChrisTitusTech/winutil/blob/main/config/tweaks.json";

    // O nome do plano é traduzido pelo Windows ("Desempenho Máximo" em pt-BR); "M.{1,2}ximo" tolera a
    // decodificação da saída do powercfg pelo console. Planos são sempre ativados/removidos pelo GUID.
    private const string UltimatePlanLookup = """
$ultimateNamePattern = '^(Ultimate Performance|Desempenho M.{1,2}ximo)$'
$ultimatePlans = @(powercfg /list | ForEach-Object {
    if ($_ -match '([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\s+\((.+?)\)') {
        [pscustomobject]@{ Guid = $Matches[1]; Name = $Matches[2] }
    }
} | Where-Object { $_.Name -match $ultimateNamePattern })

""";

    private static readonly IReadOnlyList<TweakDefinition> Definitions =
    [
        // ════════════════════════════════════════════════════════════════════
        //  ESSENTIAL TWEAKS
        // ════════════════════════════════════════════════════════════════════

        new(
            CreateRestorePointId,
            "WPFTweaksRestorePoint",
            "Criar ponto de restauração",
            "Cria um ponto do System Restore antes de mudanças futuras. Isso não garante a recuperação de todos os arquivos ou configurações.",
            "A operação usa Checkpoint-Computer com o tipo MODIFY_SETTINGS. O estado da proteção por volume não é inferido: se estiver desativada ou bloqueada, o Windows retornará erro. O LznTweaker não a habilita automaticamente nem altera a frequência global de criação.",
            TweakCategory.Essential,
            "Médio",
            "Windows 10 e Windows 11 cliente; depende da Proteção do Sistema estar disponível no volume do Windows.",
            RequiresAdministrator: true,
            RequiresRestart: false,
            HasReliableRollback: false,
            IsOneShotOperation: true,
            WinUtilSourceUrl),

        new(
            EndTaskOnTaskbarId,
            "WPFTweaksEndTaskOnTaskbar",
            "Encerrar tarefa pelo menu da barra de tarefas",
            "Adiciona a opção Encerrar tarefa ao menu de contexto de janelas na barra de tarefas do Windows 11.",
            "Grava TaskbarEndTask=1 em HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\\TaskbarDeveloperSettings. A alteração é limitada ao perfil atual e o valor anterior (inclusive ausência) será guardado para reversão.",
            TweakCategory.Essential,
            "Baixo",
            "Recurso do Windows 11; a disponibilidade do menu depende da versão do shell.",
            RequiresAdministrator: false,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            [new RegistryValueChange(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings", "TaskbarEndTask", Microsoft.Win32.RegistryValueKind.DWord, 1)]),

        new(
            DeleteTemporaryFilesId,
            "WPFTweaksDeleteTempFiles",
            "Limpar temporários do usuário",
            "Mostra uma prévia dos arquivos encontrados em %TEMP% e permite escolher exatamente quais deseja remover.",
            "Somente os arquivos selecionados na prévia, ainda existentes e sem alteração de tamanho/data desde a análise, podem ser excluídos. Arquivos em uso, diretórios e links são ignorados. A exclusão não tem reversão confiável.",
            TweakCategory.Essential,
            "Médio",
            "Pasta temporária do perfil atual; logs do Windows, Prefetch e cache do Windows Update não estão incluídos nesta etapa.",
            RequiresAdministrator: false,
            RequiresRestart: false,
            HasReliableRollback: false,
            IsOneShotOperation: true,
            WinUtilSourceUrl),

        new(
            DisableTelemetryId,
            "WPFTweaksTele",
            "Desativar Telemetria",
            "Reduz a coleta de dados de diagnóstico e uso pelo Windows, desativando serviços e tarefas agendadas de telemetria.",
            "Define AllowTelemetry=0 em duas chaves de políticas HKLM, desativa os serviços DiagTrack e dmwappushservice, e desabilita tarefas agendadas de telemetria. Cada valor, tipo de inicialização e tarefa é verificado após a alteração, e o estado anterior é guardado para o Undo. Tarefas ou serviços ausentes nesta versão são apenas informados. Em PCs gerenciados (Intune/MDM), dmwappushservice pode ser necessário.",
            TweakCategory.Essential,
            "Médio",
            "Windows 10/11. Algumas edições Enterprise/Education já podem ter telemetria restrita por política.",
            RequiresAdministrator: true,
            RequiresRestart: true,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            RestartNote: "Reinicialização recomendada para efetivar completamente.",
            HklmRegistryChanges:
            [
                new(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", Microsoft.Win32.RegistryValueKind.DWord, 0),
                new(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", Microsoft.Win32.RegistryValueKind.DWord, 0)
            ],
            ServiceChanges:
            [
                new("DiagTrack", "Disabled"),
                new("dmwappushservice", "Disabled")
            ],
            ScheduledTasksToDisable:
            [
                @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
                @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
                @"\Microsoft\Windows\Autochk\Proxy",
                @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
                @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
                @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector",
                @"\Microsoft\Windows\Feedback\Siuf\DmClient",
                @"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload",
                @"\Microsoft\Windows\Windows Error Reporting\QueueReporting",
                @"\Microsoft\Windows\Application Experience\MareBackup",
                @"\Microsoft\Windows\Application Experience\StartupAppTask",
                @"\Microsoft\Windows\Application Experience\PcaPatchDbTask",
                @"\Microsoft\Windows\Maps\MapsUpdateTask",
                @"\Microsoft\Windows\Maps\MapsToastTask"
            ]),

        new(
            DisableConsumerFeaturesId,
            "WPFTweaksConsumerFeatures",
            "Desativar Consumer Features",
            "Impede que o Windows instale automaticamente aplicativos sugeridos e promovidos da Microsoft Store.",
            "Define DisableWindowsConsumerFeatures=1 em HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent. Requer administrador. O valor anterior é guardado para reversão.",
            TweakCategory.Essential,
            "Baixo",
            "Windows 10/11 Pro, Enterprise e Education. Edições Home podem ignorar esta política.",
            RequiresAdministrator: true,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            HklmRegistryChanges:
            [
                new(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", Microsoft.Win32.RegistryValueKind.DWord, 1)
            ]),

        new(
            DisableActivityHistoryId,
            "WPFTweaksAH",
            "Desativar Activity History",
            "Impede que o Windows colete e envie o histórico de atividades do usuário para a Microsoft.",
            "Define EnableActivityFeed=0, PublishUserActivities=0 e UploadUserActivities=0 em HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System. Requer administrador.",
            TweakCategory.Essential,
            "Baixo",
            "Windows 10/11. A Timeline foi removida no Windows 11 mas os dados ainda podem ser coletados.",
            RequiresAdministrator: true,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            HklmRegistryChanges:
            [
                new(@"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", Microsoft.Win32.RegistryValueKind.DWord, 0),
                new(@"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", Microsoft.Win32.RegistryValueKind.DWord, 0),
                new(@"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", Microsoft.Win32.RegistryValueKind.DWord, 0)
            ]),

        new(
            DisableGameDvrId,
            "WPFTweaksDVR",
            "Desativar Game DVR",
            "Desativa a gravação em segundo plano de jogos (Game DVR/Game Bar Recording), reduzindo uso de GPU e disco durante jogos.",
            "Define GameDVR_Enabled=0 em HKCU e AllowGameDVR=0 em HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\GameDVR. A Game Bar permanece acessível; apenas a gravação automática é desativada.",
            TweakCategory.Essential,
            "Baixo",
            "Windows 10/11. A disponibilidade do Game DVR depende de drivers compatíveis.",
            RequiresAdministrator: true,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            RegistryChanges:
            [
                new(GameConfigStorePath, "GameDVR_Enabled", Microsoft.Win32.RegistryValueKind.DWord, 0)
            ],
            HklmRegistryChanges:
            [
                new(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", Microsoft.Win32.RegistryValueKind.DWord, 0)
            ]),

        new(
            DisableHibernationId,
            "WPFTweaksHiber",
            "Desativar Hibernação",
            "Desativa a hibernação e remove o arquivo hiberfil.sys, liberando espaço equivalente à RAM instalada.",
            "Executa powercfg /h off e confirma HibernateEnabled=0. O Windows remove o hiberfil.sys automaticamente. A Inicialização Rápida (Fast Startup) também deixa de funcionar. O app não reverte automaticamente; para reativar, use powercfg /h on.",
            TweakCategory.Essential,
            "Médio",
            "Windows 10/11. Em laptops, a hibernação preserva a sessão quando a bateria acaba; avalie antes de desativar.",
            RequiresAdministrator: true,
            RequiresRestart: false,
            HasReliableRollback: false,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            InvokeScript: "powercfg /h off",
            UndoScript: "powercfg /h on",
            VerifyScript: "(Get-ItemProperty -LiteralPath 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Power' -Name HibernateEnabled -ErrorAction Stop).HibernateEnabled -eq 0"),

        new(
            SetIpv4PriorityId,
            "WPFTweaksIPv4",
            "Preferir IPv4 sobre IPv6",
            "Configura o Windows para preferir conexões IPv4 quando ambos os protocolos estiverem disponíveis.",
            "Define DisabledComponents=0x20 (32) em HKLM\\SYSTEM\\CurrentControlSet\\Services\\Tcpip6\\Parameters. Isso não desativa o IPv6, apenas prioriza o IPv4 na resolução DNS.",
            TweakCategory.Essential,
            "Baixo",
            "Windows 10/11. Não afeta a conectividade IPv6, apenas a ordem de preferência.",
            RequiresAdministrator: true,
            RequiresRestart: true,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            RestartNote: "Reinicialização necessária para aplicar a mudança de prioridade.",
            HklmRegistryChanges:
            [
                new(@"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters", "DisabledComponents", Microsoft.Win32.RegistryValueKind.DWord, 0x20)
            ]),

        new(
            DisableLocationTrackingId,
            "WPFTweaksLoc",
            "Desativar rastreamento de localização",
            "Impede que o Windows e aplicativos acessem a localização do dispositivo por padrão.",
            "Define Value=\"Deny\" em HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\location. Aplicativos individuais podem ser reconfigurados nas Configurações.",
            TweakCategory.Essential,
            "Baixo",
            "Windows 10/11. Aplicativos que dependem de localização (mapas, clima) podem perder funcionalidade.",
            RequiresAdministrator: true,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            HklmRegistryChanges:
            [
                new(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "Value", Microsoft.Win32.RegistryValueKind.String, "Deny")
            ]),

        new(
            DisableStorageSenseId,
            "WPFTweaksStorage",
            "Desativar Storage Sense",
            "Impede que o Windows exclua automaticamente arquivos temporários e da Lixeira sem intervenção do usuário.",
            "Define 01=0 em HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\StorageSense\\Parameters\\StoragePolicy. A limpeza manual não é afetada.",
            TweakCategory.Essential,
            "Baixo",
            "Windows 10/11. Aplicado apenas ao perfil atual.",
            RequiresAdministrator: false,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            RegistryChanges:
            [
                new(@"SOFTWARE\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy", "01", Microsoft.Win32.RegistryValueKind.DWord, 0)
            ]),

        new(
            DisableRecallId,
            "WPFTweaksRecall",
            "Desativar Recall",
            "Impede que o Windows capture e armazene automaticamente capturas de tela para o recurso Recall (AI).",
            "Define DisableAIDataAnalysis=1 em HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI. Requer administrador. Recall ainda é um recurso em prévia e pode não estar disponível em todos os dispositivos.",
            TweakCategory.Essential,
            "Baixo",
            "Windows 11 24H2+. Em versões sem Recall, a chave é criada sem efeito colateral.",
            RequiresAdministrator: true,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            HklmRegistryChanges:
            [
                new(@"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", Microsoft.Win32.RegistryValueKind.DWord, 1)
            ]),

        // ════════════════════════════════════════════════════════════════════
        //  CUSTOMIZE PREFERENCES
        // ════════════════════════════════════════════════════════════════════

        new(
            DarkThemeId,
            "WPFToggleDarkMode",
            "Tema escuro do Windows",
            "Ativa o tema escuro para o Windows e aplicativos compatíveis neste perfil.",
            "Define AppsUseLightTheme e SystemUsesLightTheme como 0 em HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize. O estado só é considerado ativo se os dois valores forem lidos como 0; valores mistos ou ausentes são exibidos como estado parcial/desconhecido.",
            TweakCategory.CustomizePreferences,
            "Baixo",
            "Windows 10/11; algumas janelas abertas podem precisar ser reabertas para atualizar a aparência.",
            RequiresAdministrator: false,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            [
                new RegistryValueChange(ThemePersonalizePath, "AppsUseLightTheme", Microsoft.Win32.RegistryValueKind.DWord, 0),
                new RegistryValueChange(ThemePersonalizePath, "SystemUsesLightTheme", Microsoft.Win32.RegistryValueKind.DWord, 0)
            ]),

        new(
            ShowFileExtensionsId,
            "WPFToggleShowExt",
            "Mostrar extensões de arquivo",
            "Exibe extensões como .exe, .png e .txt no Explorador de Arquivos.",
            "Define HideFileExt=0 no perfil atual. Não altera os arquivos. Para refletir a opção em todas as janelas, pode ser necessário reiniciar manualmente o Explorador de Arquivos; o app não o encerra sem consentimento.",
            TweakCategory.CustomizePreferences,
            "Baixo",
            "Windows 10/11; aplicado somente ao perfil atual.",
            RequiresAdministrator: false,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            [new RegistryValueChange(ExplorerAdvancedPath, "HideFileExt", Microsoft.Win32.RegistryValueKind.DWord, 0)],
            RestartNote: "Pode exigir reinício manual do Explorador"),

        new(
            DisableBingSearchId,
            "WPFToggleBingSearch",
            "Desativar pesquisa Bing no menu Iniciar",
            "Impede que o menu Iniciar envie consultas de pesquisa para o Bing, limitando os resultados a itens locais.",
            "Define DisableSearchBoxSuggestions=1 em HKCU\\SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer. Aplicado ao perfil atual.",
            TweakCategory.CustomizePreferences,
            "Baixo",
            "Windows 10/11. Em edições Home, o efeito pode ser limitado em versões futuras.",
            RequiresAdministrator: false,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            [new RegistryValueChange(@"SOFTWARE\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", Microsoft.Win32.RegistryValueKind.DWord, 1)]),

        new(
            NumLockOnStartupId,
            "WPFToggleNumLock",
            "Ativar NumLock na inicialização",
            "Liga o NumLock automaticamente ao iniciar o Windows.",
            "Define InitialKeyboardIndicators=\"2\" em HKCU\\Control Panel\\Keyboard. Valor REG_SZ. O estado anterior é guardado para reversão.",
            TweakCategory.CustomizePreferences,
            "Baixo",
            "Windows 10/11. Algumas BIOS/UEFI podem sobrescrever esta configuração.",
            RequiresAdministrator: false,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            [new RegistryValueChange(@"Control Panel\Keyboard", "InitialKeyboardIndicators", Microsoft.Win32.RegistryValueKind.String, "2")]),

        new(
            VerboseLogonMessagesId,
            "WPFToggleVerboseLogon",
            "Mensagens detalhadas de logon",
            "Mostra mensagens detalhadas durante a inicialização, logon e desligamento do Windows.",
            "Define VerboseStatus=1 em HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System. Requer administrador.",
            TweakCategory.CustomizePreferences,
            "Baixo",
            "Windows 10/11. Substitui as mensagens genéricas como 'Preparando o Windows' por detalhes dos serviços sendo iniciados.",
            RequiresAdministrator: true,
            RequiresRestart: true,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            RestartNote: "Visível a partir da próxima inicialização.",
            HklmRegistryChanges:
            [
                new(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "VerboseStatus", Microsoft.Win32.RegistryValueKind.DWord, 1)
            ]),

        new(
            DisableSnapWindowId,
            "WPFToggleSnapWindow",
            "Desativar Snap Window",
            "Impede que janelas sejam redimensionadas automaticamente ao arrastá-las para as bordas da tela.",
            "Define WindowArrangementActive=\"0\" em HKCU\\Control Panel\\Desktop. Valor REG_SZ.",
            TweakCategory.CustomizePreferences,
            "Baixo",
            "Windows 10/11. Aplicado ao perfil atual.",
            RequiresAdministrator: false,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            [new RegistryValueChange(@"Control Panel\Desktop", "WindowArrangementActive", Microsoft.Win32.RegistryValueKind.String, "0")]),

        new(
            DisableSnapAssistId,
            "WPFToggleSnapAssist",
            "Desativar Snap Assist",
            "Remove o painel de sugestão que aparece ao encaixar uma janela, pedindo para escolher outra janela para o lado oposto.",
            "Define EnableSnapAssistFlyout=0 e SnapAssist=0 em HKCU\\Explorer\\Advanced.",
            TweakCategory.CustomizePreferences,
            "Baixo",
            "Windows 10/11. O Snap Window manual (arrastar para a borda) não é afetado.",
            RequiresAdministrator: false,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            [
                new RegistryValueChange(ExplorerAdvancedPath, "EnableSnapAssistFlyout", Microsoft.Win32.RegistryValueKind.DWord, 0),
                new RegistryValueChange(ExplorerAdvancedPath, "SnapAssist", Microsoft.Win32.RegistryValueKind.DWord, 0)
            ]),

        new(
            DisableMouseAccelerationId,
            "WPFToggleMouseAcceleration",
            "Desativar aceleração do mouse",
            "Remove a aceleração do ponteiro (Enhance pointer precision), tornando o movimento linear e previsível.",
            "Define MouseSpeed=\"0\", MouseThreshold1=\"0\" e MouseThreshold2=\"0\" em HKCU\\Control Panel\\Mouse. Valores REG_SZ. Recomendado para jogos FPS.",
            TweakCategory.CustomizePreferences,
            "Baixo",
            "Windows 10/11. Aplicado ao perfil atual. Pode exigir logoff para efetivar.",
            RequiresAdministrator: false,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            [
                new RegistryValueChange(@"Control Panel\Mouse", "MouseSpeed", Microsoft.Win32.RegistryValueKind.String, "0"),
                new RegistryValueChange(@"Control Panel\Mouse", "MouseThreshold1", Microsoft.Win32.RegistryValueKind.String, "0"),
                new RegistryValueChange(@"Control Panel\Mouse", "MouseThreshold2", Microsoft.Win32.RegistryValueKind.String, "0")
            ]),

        new(
            DisableStickyKeysId,
            "WPFToggleStickyKeys",
            "Desativar Sticky Keys",
            "Desativa o atalho de Sticky Keys (pressionar Shift 5 vezes) que pode interromper jogos.",
            "Define Flags=\"506\" em HKCU\\Control Panel\\Accessibility\\StickyKeys. O valor padrão é \"510\" (atalho ativado).",
            TweakCategory.CustomizePreferences,
            "Baixo",
            "Windows 10/11. Apenas o atalho é desativado; Sticky Keys pode ser ativado manualmente nas Configurações.",
            RequiresAdministrator: false,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            [new RegistryValueChange(@"Control Panel\Accessibility\StickyKeys", "Flags", Microsoft.Win32.RegistryValueKind.String, "506")]),

        new(
            ShowHiddenFilesId,
            "WPFToggleHiddenFiles",
            "Mostrar arquivos ocultos",
            "Exibe arquivos e pastas marcados como ocultos no Explorador de Arquivos.",
            "Define Hidden=1 em HKCU\\Explorer\\Advanced. Não altera os atributos dos arquivos.",
            TweakCategory.CustomizePreferences,
            "Baixo",
            "Windows 10/11. Aplicado ao perfil atual.",
            RequiresAdministrator: false,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            [new RegistryValueChange(ExplorerAdvancedPath, "Hidden", Microsoft.Win32.RegistryValueKind.DWord, 1)]),

        new(
            DisableSearchButtonId,
            "WPFToggleTaskbarSearch",
            "Desativar botão Search na barra de tarefas",
            "Remove a caixa ou ícone de pesquisa da barra de tarefas. A pesquisa continua acessível via Win+S.",
            "Define SearchboxTaskbarMode=0 em HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Search.",
            TweakCategory.CustomizePreferences,
            "Baixo",
            "Windows 10/11. Aplicado ao perfil atual.",
            RequiresAdministrator: false,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            [new RegistryValueChange(@"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", Microsoft.Win32.RegistryValueKind.DWord, 0)]),

        new(
            DisableTaskViewId,
            "WPFToggleTaskView",
            "Desativar botão Task View",
            "Remove o botão de Visão de Tarefas da barra de tarefas. O atalho Win+Tab continua funcionando.",
            "Define ShowTaskViewButton=0 em HKCU\\Explorer\\Advanced.",
            TweakCategory.CustomizePreferences,
            "Baixo",
            "Windows 10/11. Aplicado ao perfil atual.",
            RequiresAdministrator: false,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            [new RegistryValueChange(ExplorerAdvancedPath, "ShowTaskViewButton", Microsoft.Win32.RegistryValueKind.DWord, 0)]),

        new(
            DisableTaskbarWidgetsId,
            "WPFToggleTaskbarWidgets",
            "Desativar Widgets na barra de tarefas",
            "Remove o botão de Widgets da barra de tarefas do Windows 11.",
            "Define TaskbarDa=0 em HKCU\\Explorer\\Advanced.",
            TweakCategory.CustomizePreferences,
            "Baixo",
            "Windows 11. No Windows 10, a chave é criada sem efeito visual.",
            RequiresAdministrator: false,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            [new RegistryValueChange(ExplorerAdvancedPath, "TaskbarDa", Microsoft.Win32.RegistryValueKind.DWord, 0)]),

        new(
            TaskbarAlignLeftId,
            "WPFToggleTaskbarAlignment",
            "Alinhar barra de tarefas à esquerda",
            "Move os ícones da barra de tarefas do Windows 11 para o alinhamento à esquerda, como no Windows 10.",
            "Define TaskbarAl=0 em HKCU\\Explorer\\Advanced.",
            TweakCategory.CustomizePreferences,
            "Baixo",
            "Windows 11 apenas. No Windows 10, a barra já é alinhada à esquerda.",
            RequiresAdministrator: false,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            [new RegistryValueChange(ExplorerAdvancedPath, "TaskbarAl", Microsoft.Win32.RegistryValueKind.DWord, 0)]),

        // ════════════════════════════════════════════════════════════════════
        //  ADVANCED TWEAKS
        // ════════════════════════════════════════════════════════════════════

        new(
            AdobeNetworkBlockId,
            "WPFTweaksAdobeNetBlock",
            "Adobe Network Block",
            "Bloqueia conexões de ativação e telemetria da Adobe adicionando entradas ao arquivo hosts do sistema.",
            "Adiciona entradas 0.0.0.0 para hosts conhecidos de ativação/telemetria Adobe no arquivo hosts, entre marcadores LznTweaker, e confirma que o bloco foi gravado. A remoção ainda é manual (apagar o bloco marcado no arquivo hosts).",
            TweakCategory.Advanced,
            "Médio",
            "Windows 10/11. Pode impedir ativação/validação de produtos Adobe legítimos.",
            RequiresAdministrator: true,
            RequiresRestart: false,
            HasReliableRollback: false,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            InvokeScript: """
$hostsPath = Join-Path $env:SystemRoot 'System32\drivers\etc\hosts'
$marker = '# LznTweaker Adobe Block'
$endMarker = '# /LznTweaker Adobe Block'
$existing = Get-Content $hostsPath -Raw -ErrorAction SilentlyContinue
if ($existing -and $existing.Contains($marker)) { throw 'Bloqueio Adobe ja presente no arquivo hosts.' }
$adobeHosts = @('lmlicenses.wip4.adobe.com','lm.licenses.adobe.com','na1r.services.adobe.com','hlrcv.stage.adobe.com','practivate.adobe.com','activate.adobe.com','activate-sea.adobe.com','activate-sjc0.adobe.com','genuine.adobe.com','cc-api-data.adobe.io','ic-contrib.adobecc.com','ereg.wip3.adobe.com','ereg.adobe.com','wip3.adobe.com','3dns-3.adobe.com','3dns-2.adobe.com','adobe-dns.adobe.com','adobe-dns-2.adobe.com','adobe-dns-3.adobe.com')
$block = "`n$marker`n"
foreach ($h in $adobeHosts) { $block += "0.0.0.0 $h`n" }
$block += "$endMarker`n"
Add-Content -LiteralPath $hostsPath -Value $block -Encoding ASCII -ErrorAction Stop
ipconfig /flushdns | Out-Null
""",
            UndoScript: """
$hostsPath = Join-Path $env:SystemRoot 'System32\drivers\etc\hosts'
$content = Get-Content $hostsPath -Raw -ErrorAction Stop
$cleaned = $content -replace '(?ms)\r?\n# LznTweaker Adobe Block\r?\n.*?# /LznTweaker Adobe Block\r?\n?', ''
Set-Content -LiteralPath $hostsPath -Value $cleaned -Encoding ASCII -ErrorAction Stop
ipconfig /flushdns | Out-Null
""",
            VerifyScript: "(Get-Content -LiteralPath (Join-Path $env:SystemRoot 'System32\\drivers\\etc\\hosts') -Raw -ErrorAction Stop).Contains('# /LznTweaker Adobe Block')"),

        new(
            AdobeDebloatId,
            "WPFTweaksDebloatAdobe",
            "Adobe Debloat",
            "Para e desativa serviços e tarefas agendadas de atualização e telemetria da Adobe.",
            "Desativa os serviços AdobeUpdateService, AdobeARMservice, AGSService e AGMService. Desabilita tarefas agendadas na pasta \\Adobe. Serviços ausentes são ignorados. O resultado não é verificado item a item e o estado anterior não é guardado.",
            TweakCategory.Advanced,
            "Médio",
            "Windows 10/11. Somente afeta produtos Adobe instalados. Atualizações automáticas da Adobe serão desativadas.",
            RequiresAdministrator: true,
            RequiresRestart: false,
            HasReliableRollback: false,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            InvokeScript: """
$svcs = @('AdobeUpdateService','AdobeARMservice','AGSService','AGMService','AdobeFlashPlayerUpdateSvc')
foreach ($s in $svcs) {
    $svc = Get-Service -Name $s -ErrorAction SilentlyContinue
    if ($svc) { Stop-Service $s -Force -ErrorAction SilentlyContinue; Set-Service $s -StartupType Disabled -ErrorAction SilentlyContinue }
}
Get-ScheduledTask -TaskPath '\Adobe*' -ErrorAction SilentlyContinue | Disable-ScheduledTask -ErrorAction SilentlyContinue
""",
            UndoScript: """
$svcs = @('AdobeUpdateService','AdobeARMservice','AGSService','AGMService','AdobeFlashPlayerUpdateSvc')
foreach ($s in $svcs) {
    $svc = Get-Service -Name $s -ErrorAction SilentlyContinue
    if ($svc) { Set-Service $s -StartupType Automatic -ErrorAction SilentlyContinue; Start-Service $s -ErrorAction SilentlyContinue }
}
Get-ScheduledTask -TaskPath '\Adobe*' -ErrorAction SilentlyContinue | Enable-ScheduledTask -ErrorAction SilentlyContinue
"""),

        new(
            DisableIpv6Id,
            "WPFTweaksDisableIPv6",
            "Desativar IPv6",
            "Desativa completamente o protocolo IPv6 em todas as interfaces de rede. Use apenas se sua rede não requer IPv6.",
            "Define DisabledComponents=0xFF (255) em HKLM\\SYSTEM\\CurrentControlSet\\Services\\Tcpip6\\Parameters. Diferente da priorização IPv4, isso desativa o IPv6 totalmente.",
            TweakCategory.Advanced,
            "Alto",
            "Windows 10/11. Pode causar problemas de conectividade em redes que dependem de IPv6 (incluindo alguns provedores).",
            RequiresAdministrator: true,
            RequiresRestart: true,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            RestartNote: "Reinicialização obrigatória para desativar o IPv6.",
            HklmRegistryChanges:
            [
                new(@"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters", "DisabledComponents", Microsoft.Win32.RegistryValueKind.DWord, 0xFF)
            ]),

        new(
            DisableTeredoId,
            "WPFTweaksTeredo",
            "Desativar Teredo",
            "Desativa o túnel Teredo (transição IPv4→IPv6). Pode melhorar latência em jogos online que não usam IPv6.",
            "Executa netsh interface teredo set state disabled e confirma com Get-NetTeredoConfiguration. O app não reverte automaticamente; para restaurar, use netsh interface teredo set state default.",
            TweakCategory.Advanced,
            "Médio",
            "Windows 10/11. Alguns jogos da Xbox/Microsoft Store podem usar Teredo para NAT traversal.",
            RequiresAdministrator: true,
            RequiresRestart: false,
            HasReliableRollback: false,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            InvokeScript: "netsh interface teredo set state disabled",
            UndoScript: "netsh interface teredo set state default",
            VerifyScript: "[string](Get-NetTeredoConfiguration -ErrorAction Stop).Type -eq 'Disabled'"),

        new(
            DisableBackgroundAppsId,
            "WPFTweaksDisableBGapps",
            "Desativar aplicativos em segundo plano",
            "Impede que aplicativos UWP/Store executem tarefas em segundo plano globalmente.",
            "Define GlobalUserDisabled=1 em HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications. Aplicativos individuais podem ser reconfigurados nas Configurações.",
            TweakCategory.Advanced,
            "Médio",
            "Windows 10/11. Notificações e atualizações de aplicativos UWP podem ser afetadas.",
            RequiresAdministrator: false,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            [new RegistryValueChange(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", Microsoft.Win32.RegistryValueKind.DWord, 1)]),

        new(
            DisableFullscreenOptId,
            "WPFTweaksDisableFSO",
            "Desativar Fullscreen Optimizations",
            "Desativa otimizações de tela cheia do Windows, forçando jogos a usar fullscreen exclusivo real.",
            "Define GameDVR_FSEBehaviorMode=2 em HKCU\\System\\GameConfigStore. Pode melhorar latência de input em alguns jogos mais antigos.",
            TweakCategory.Advanced,
            "Baixo",
            "Windows 10/11. Jogos modernos (DX12) geralmente funcionam melhor com as otimizações ativadas.",
            RequiresAdministrator: false,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            [new RegistryValueChange(GameConfigStorePath, "GameDVR_FSEBehaviorMode", Microsoft.Win32.RegistryValueKind.DWord, 2)]),

        new(
            DisableCopilotId,
            "WPFTweaksRemoveCopilot",
            "Desativar Copilot",
            "Desativa o Windows Copilot (assistente AI) via política de grupo.",
            "Define TurnOffWindowsCopilot=1 em HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsCopilot. Requer administrador.",
            TweakCategory.Advanced,
            "Baixo",
            "Windows 11 23H2+. Em versões sem Copilot, a chave é criada sem efeito colateral.",
            RequiresAdministrator: true,
            RequiresRestart: false,
            HasReliableRollback: true,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            HklmRegistryChanges:
            [
                new(@"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", Microsoft.Win32.RegistryValueKind.DWord, 1)
            ]),

        // ════════════════════════════════════════════════════════════════════
        //  PERFORMANCE PLANS
        // ════════════════════════════════════════════════════════════════════

        new(
            ActivateUltimatePerformanceId,
            "WPFPowerPlanUltimate",
            "Ativar Ultimate Performance",
            "Cria e ativa o plano de energia Ultimate Performance, que desativa gerenciamento de energia agressivo.",
            "Se já existir um plano Ultimate Performance (\"Desempenho Máximo\" em pt-BR), ativa o existente pelo GUID; caso contrário, cria uma única cópia com powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61. O plano ativo é confirmado depois. Não altera configurações individuais do plano.",
            TweakCategory.PerformancePlans,
            "Médio",
            "Windows 10 1803+ e Windows 11. Não recomendado para laptops (WinUtil: \"NOT FOR LAPTOPS\"): aumenta consumo e temperatura sem ganho garantido.",
            RequiresAdministrator: true,
            RequiresRestart: false,
            HasReliableRollback: false,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            InvokeScript: UltimatePlanLookup + """
if ($ultimatePlans.Count -gt 0) {
    $guid = $ultimatePlans[0].Guid
} else {
    $output = powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61
    if ($LASTEXITCODE -ne 0) { throw "powercfg nao criou o plano: $output" }
    $guidMatch = [regex]::Match([string]$output, '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}')
    if (-not $guidMatch.Success) { throw "Nao foi possivel identificar o plano criado: $output" }
    $guid = $guidMatch.Value
}
powercfg /setactive $guid
if ($LASTEXITCODE -ne 0) { throw "powercfg nao ativou o plano $guid." }
""",
            VerifyScript: UltimatePlanLookup + """
$active = [string](powercfg /getactivescheme)
@($ultimatePlans | Where-Object { $active -match $_.Guid }).Count -gt 0
"""),

        new(
            RemoveUltimatePerformanceId,
            "WPFPowerPlanRemoveUltimate",
            "Remover Ultimate Performance",
            "Remove todas as cópias do plano Ultimate Performance e, se uma delas estiver ativa, ativa o plano Equilibrado.",
            "Localiza os planos Ultimate Performance/\"Desempenho Máximo\" via powercfg /list, ativa o Equilibrado (381b4222-f694-41f0-9685-ff5bb260df2e) se necessário e exclui cada cópia pelo GUID. Outros planos personalizados não são tocados (ao contrário de powercfg /restoredefaultschemes). Se nenhum plano existir, nada é alterado.",
            TweakCategory.PerformancePlans,
            "Médio",
            "Windows 10/11. Configurações feitas manualmente dentro do plano removido são perdidas.",
            RequiresAdministrator: true,
            RequiresRestart: false,
            HasReliableRollback: false,
            IsOneShotOperation: false,
            WinUtilSourceUrl,
            InvokeScript: UltimatePlanLookup + """
if ($ultimatePlans.Count -eq 0) { throw 'Nenhum plano Ultimate Performance encontrado; nada foi alterado.' }
$balanced = '381b4222-f694-41f0-9685-ff5bb260df2e'
$active = [string](powercfg /getactivescheme)
if (@($ultimatePlans | Where-Object { $active -match $_.Guid }).Count -gt 0) {
    powercfg /setactive $balanced
    if ($LASTEXITCODE -ne 0) { throw 'Nao foi possivel ativar o plano Equilibrado; nada foi removido.' }
}
foreach ($plan in $ultimatePlans) {
    powercfg /delete $plan.Guid
    if ($LASTEXITCODE -ne 0) { throw "powercfg nao removeu o plano $($plan.Guid)." }
}
""",
            VerifyScript: UltimatePlanLookup + """
$ultimatePlans.Count -eq 0
""")
    ];

    public static IReadOnlyList<TweakDefinition> All => Definitions;

    public static TweakDefinition GetById(string id) =>
        Definitions.FirstOrDefault(definition => string.Equals(definition.Id, id, StringComparison.Ordinal))
        ?? throw new KeyNotFoundException($"Tweak não encontrado no catálogo: {id}");
}

/// <summary>
/// Presets derivados de config/preset.json do WinUtil, limitados aos itens implementados neste catálogo.
/// Itens do WinUtil ainda ausentes aqui (WPBT, Services, Delivery Optimization, Disk Cleanup, Explorer
/// AutoDiscovery, remoção de Widgets/OneDrive, etc.) não são substituídos por equivalentes inventados.
/// </summary>
public static class TweakPresetCatalog
{
    private static readonly string[] StandardSelection =
    [
        TweakCatalog.CreateRestorePointId,      // WPFTweaksRestorePoint
        TweakCatalog.DisableActivityHistoryId,  // WPFTweaksActivity
        TweakCatalog.DisableConsumerFeaturesId, // WPFTweaksConsumerFeatures
        TweakCatalog.DisableLocationTrackingId, // WPFTweaksLocation
        TweakCatalog.DisableTelemetryId,        // WPFTweaksTelemetry
        TweakCatalog.DeleteTemporaryFilesId,    // WPFTweaksDeleteTempFiles
        TweakCatalog.EndTaskOnTaskbarId         // WPFTweaksEndTaskOnTaskbar
    ];

    private static readonly IReadOnlyDictionary<TweakPreset, IReadOnlySet<string>> SelectionByPreset =
        new Dictionary<TweakPreset, IReadOnlySet<string>>
        {
            // WinUtil Minimal = ConsumerFeatures, WPBT, Services, Telemetry. O ponto de restauração é
            // mantido aqui de propósito, para que nenhum preset altere o sistema sem tentar criá-lo antes.
            [TweakPreset.Minimal] = new HashSet<string>(
            [
                TweakCatalog.CreateRestorePointId,
                TweakCatalog.DisableConsumerFeaturesId,
                TweakCatalog.DisableTelemetryId
            ], StringComparer.Ordinal),

            [TweakPreset.Standard] = new HashSet<string>(StandardSelection, StringComparer.Ordinal),

            // WinUtil Advanced = Standard + itens de IA (WPFTweaksWindowsAI), entre outros ainda não implementados.
            [TweakPreset.Advanced] = new HashSet<string>(
            [
                ..StandardSelection,
                TweakCatalog.DisableRecallId,
                TweakCatalog.DisableCopilotId
            ], StringComparer.Ordinal),

            [TweakPreset.Custom] = new HashSet<string>(StringComparer.Ordinal)
        };

    public static IReadOnlySet<string> GetSelection(TweakPreset preset) => SelectionByPreset[preset];

    public static TweakPreset MatchImplementedSelection(IEnumerable<string> selectedIds)
    {
        var selected = new HashSet<string>(selectedIds, StringComparer.Ordinal);
        if (selected.Count == 0)
            return TweakPreset.Custom;
        foreach (var preset in new[] { TweakPreset.Minimal, TweakPreset.Standard, TweakPreset.Advanced })
        {
            if (selected.SetEquals(SelectionByPreset[preset]))
                return preset;
        }

        return TweakPreset.Custom;
    }
}
