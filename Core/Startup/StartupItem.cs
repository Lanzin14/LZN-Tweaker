namespace LznTweaker.Core.Startup;

public enum StartupLocation
{
    CurrentUserRegistry,    // HKCU\...\Run
    LocalMachineRegistry,   // HKLM\...\Run
    UserStartupFolder,      // %APPDATA%\...\Startup
    CommonStartupFolder     // %PROGRAMDATA%\...\Startup
}

public enum StartupCategory
{
    GameLauncher,
    HardwareAndPeripherals,
    CommunicationAndChat,
    MediaAndAudio,
    CloudAndSync,
    SystemUtility
}

public sealed class StartupItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Command { get; init; }
    public string? ExecutablePath { get; init; }
    public required StartupLocation Location { get; init; }
    public required string LocationKeyName { get; init; }
    /// <summary>Chave StartupApproved que o Windows (Gerenciador de Tarefas) consulta para este item.</summary>
    public required string ApprovedSubKey { get; init; }
    public required bool ApprovedInLocalMachine { get; init; }
    public required StartupCategory Category { get; init; }
    public required string Publisher { get; init; }
    public required string Impact { get; init; } // "Alto", "Médio", "Baixo"
    public bool IsEnabled { get; set; }

    public string LocationDisplay => Location switch
    {
        StartupLocation.CurrentUserRegistry => "HKCU (Usuário atual)",
        StartupLocation.LocalMachineRegistry => "HKLM (Sistema)",
        StartupLocation.UserStartupFolder => "Pasta Inicializar (Usuário)",
        StartupLocation.CommonStartupFolder => "Pasta Inicializar (Geral)",
        _ => "Desconhecido"
    };

    public string CategoryDisplay => Category switch
    {
        StartupCategory.GameLauncher => "Launcher de Jogos",
        StartupCategory.HardwareAndPeripherals => "Periféricos & Hardware",
        StartupCategory.CommunicationAndChat => "Comunicação & Chat",
        StartupCategory.MediaAndAudio => "Mídia & Áudio",
        StartupCategory.CloudAndSync => "Nuvem & Sincronização",
        _ => "Utilitário do Sistema"
    };

    public string CategoryGlyph => Category switch
    {
        StartupCategory.GameLauncher => "🎮",
        StartupCategory.HardwareAndPeripherals => "⌨",
        StartupCategory.CommunicationAndChat => "💬",
        StartupCategory.MediaAndAudio => "🎵",
        StartupCategory.CloudAndSync => "☁",
        _ => "⚙"
    };
}
