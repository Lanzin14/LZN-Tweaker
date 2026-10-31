using System.IO;
using System.Text.Json;
using LznTweaker.Core.GameTuning;

namespace LznTweaker.Core.GameOptimization;

public enum GamePlatform { Steam, Riot, Epic }

public sealed record SupportedGame(
    string Id,
    string Name,
    GamePlatform Platform,
    string? SteamAppId,
    /// <summary>Executável relativo à pasta de instalação.</summary>
    string ExecutableRelativePath,
    /// <summary>Nome do processo (sem .exe) usado para saber se o jogo está aberto.</summary>
    string ProcessName);

public sealed record DetectedGame(SupportedGame Game, string? InstallPath, string? ExecutablePath, string? SteamLibraryPath)
{
    public bool IsInstalled => InstallPath is not null;
}

/// <summary>Jogos com página de otimização. Cada um tem seus próprios caminhos: nada de um é usado em outro.</summary>
public static class SupportedGames
{
    public const string Cs2Id = "cs2";
    public const string ValorantId = "valorant";
    public const string Overwatch2Id = "overwatch2";
    public const string DeadlockId = "deadlock";
    public const string FortniteId = "fortnite";

    public static readonly SupportedGame Cs2 = new(Cs2Id, "Counter-Strike 2", GamePlatform.Steam, "730",
        @"game\bin\win64\cs2.exe", "cs2");
    public static readonly SupportedGame Valorant = new(ValorantId, "VALORANT", GamePlatform.Riot, null,
        @"ShooterGame\Binaries\Win64\VALORANT-Win64-Shipping.exe", "VALORANT-Win64-Shipping");
    public static readonly SupportedGame Overwatch2 = new(Overwatch2Id, "Overwatch 2 (Steam)", GamePlatform.Steam, "2357570",
        "Overwatch.exe", "Overwatch");
    public static readonly SupportedGame Deadlock = new(DeadlockId, "Deadlock", GamePlatform.Steam, DeadlockProfiles.SteamAppId,
        @"game\bin\win64\deadlock.exe", "deadlock");

    /// <summary>Fortnite pelo Epic Games Launcher (AppName "Fortnite" no LauncherInstalled.dat).</summary>
    public static readonly SupportedGame Fortnite = new(FortniteId, "Fortnite", GamePlatform.Epic, null,
        @"FortniteGame\Binaries\Win64\FortniteClient-Win64-Shipping.exe", "FortniteClient-Win64-Shipping");

    public static IReadOnlyList<SupportedGame> All { get; } = [Cs2, Valorant, Overwatch2, Deadlock, Fortnite];
}

/// <summary>Detecta a instalação real de cada jogo (bibliotecas da Steam, metadados do Riot Client ou lista do Epic Games Launcher).</summary>
public sealed class GameLocator
{
    private readonly SteamLibraryLocator _steam;
    private readonly string _riotMetadataRoot;
    private readonly string _epicInstalledList;

    public GameLocator(SteamLibraryLocator steam)
        : this(steam,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Riot Games", "Metadata"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "UnrealEngineLauncher", "LauncherInstalled.dat")) { }

    internal GameLocator(SteamLibraryLocator steam, string riotMetadataRoot, string epicInstalledList)
    {
        _steam = steam;
        _riotMetadataRoot = riotMetadataRoot;
        _epicInstalledList = epicInstalledList;
    }

    public SteamLibraryLocator Steam => _steam;

    public DetectedGame Locate(SupportedGame game)
    {
        string? installPath;
        string? library = null;
        if (game.Platform == GamePlatform.Steam && game.SteamAppId is { } appId)
        {
            installPath = _steam.FindAppInstallPath(appId);
            library = installPath is null ? null : _steam.FindAppLibraryPath(appId);
        }
        else if (game.Platform == GamePlatform.Epic)
        {
            installPath = FindEpicInstall("Fortnite");
        }
        else
        {
            installPath = FindValorantInstall();
        }

        var executable = installPath is null ? null : Path.Combine(installPath, game.ExecutableRelativePath);
        return new DetectedGame(game, installPath, executable is not null && File.Exists(executable) ? executable : null, library);
    }

    /// <summary>
    /// O Epic Games Launcher lista as instalações em ProgramData\Epic\UnrealEngineLauncher\LauncherInstalled.dat (JSON:
    /// InstallationList[].AppName e InstallLocation). Somente leitura: o app não altera o launcher nem o jogo.
    /// </summary>
    private string? FindEpicInstall(string appName)
    {
        if (!File.Exists(_epicInstalledList))
            return null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(_epicInstalledList));
            if (!document.RootElement.TryGetProperty("InstallationList", out var list) || list.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var entry in list.EnumerateArray())
            {
                if (entry.TryGetProperty("AppName", out var name) && name.GetString() == appName &&
                    entry.TryGetProperty("InstallLocation", out var location) && location.GetString() is { Length: > 0 } path)
                    return Directory.Exists(path) ? Path.GetFullPath(path) : null;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Lista ilegível: tratado como não instalado.
        }
        return null;
    }

    /// <summary>
    /// O Riot Client registra a pasta do VALORANT em Metadata\valorant.live\valorant.live.product_settings.yaml
    /// (chave product_install_full_path). Nada do Vanguard é lido ou tocado.
    /// </summary>
    private string? FindValorantInstall()
    {
        var settings = Path.Combine(_riotMetadataRoot, "valorant.live", "valorant.live.product_settings.yaml");
        if (!File.Exists(settings))
            return null;
        try
        {
            foreach (var line in File.ReadLines(settings))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("product_install_full_path:", StringComparison.Ordinal))
                    continue;
                var value = trimmed["product_install_full_path:".Length..].Trim().Trim('"', '\'').Replace('/', '\\');
                return Directory.Exists(value) ? Path.GetFullPath(value) : null;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Metadados ilegíveis: tratado como não instalado.
        }
        return null;
    }
}
