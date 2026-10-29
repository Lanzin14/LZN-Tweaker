using System.IO;
using System.Security;
using Microsoft.Win32;

namespace LznTweaker.Core.GameTuning;

/// <summary>Localiza jogos instalados pela Steam a partir do registro, do libraryfolders.vdf e do appmanifest.</summary>
public sealed class SteamLibraryLocator
{
    private readonly Func<string?> _steamRootProvider;

    public SteamLibraryLocator() : this(ReadSteamRootFromRegistry) { }

    internal SteamLibraryLocator(Func<string?> steamRootProvider) => _steamRootProvider = steamRootProvider;

    /// <summary>Pasta da Steam (onde ficam userdata e config), ou null se a Steam não estiver instalada.</summary>
    public string? SteamRoot
    {
        get
        {
            var root = _steamRootProvider();
            return string.IsNullOrWhiteSpace(root) || !Directory.Exists(root) ? null : Path.GetFullPath(root);
        }
    }

    /// <summary>Pasta de instalação do app (ex.: ...\steamapps\common\Deadlock), ou null se não encontrado.</summary>
    public string? FindAppInstallPath(string appId) => FindApp(appId)?.InstallPath;

    /// <summary>Biblioteca Steam que contém o app (onde ficam steamapps\shadercache\&lt;AppID&gt;), ou null.</summary>
    public string? FindAppLibraryPath(string appId) => FindApp(appId)?.LibraryPath;

    private (string LibraryPath, string InstallPath)? FindApp(string appId)
    {
        var steamRoot = SteamRoot;
        if (steamRoot is null)
            return null;

        foreach (var library in GetLibraryFolders(steamRoot))
        {
            var manifest = Path.Combine(library, "steamapps", $"appmanifest_{appId}.acf");
            if (!File.Exists(manifest))
                continue;
            try
            {
                var installDir = KeyValuesReader.Parse(File.ReadAllText(manifest))
                    .FirstOrDefault(node => node.Key.Equals("AppState", StringComparison.OrdinalIgnoreCase))
                    ?.ChildValue("installdir");
                if (string.IsNullOrWhiteSpace(installDir) || installDir.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                    continue;
                var installPath = Path.Combine(library, "steamapps", "common", installDir);
                if (Directory.Exists(installPath))
                    return (library, Path.GetFullPath(installPath));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Biblioteca inacessível: tenta a próxima.
            }
        }
        return null;
    }

    internal static IReadOnlyList<string> GetLibraryFolders(string steamRoot)
    {
        var libraries = new List<string> { Path.GetFullPath(steamRoot) };
        var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf))
            return libraries;
        try
        {
            var root = KeyValuesReader.Parse(File.ReadAllText(vdf))
                .FirstOrDefault(node => node.Key.Equals("libraryfolders", StringComparison.OrdinalIgnoreCase));
            foreach (var entry in root?.Children ?? [])
            {
                // Formato atual: "0" { "path" "D:\\SteamLibrary" ... }; formato antigo: "1" "D:\\SteamLibrary".
                var path = entry.ChildValue("path") ?? entry.Value;
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                {
                    var full = Path.GetFullPath(path);
                    if (!libraries.Contains(full, StringComparer.OrdinalIgnoreCase))
                        libraries.Add(full);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sem a lista de bibliotecas, ainda vale a biblioteca padrão.
        }
        return libraries;
    }

    private static string? ReadSteamRootFromRegistry()
    {
        try
        {
            using var user = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (user?.GetValue("SteamPath") is string steamPath && Directory.Exists(steamPath))
                return Path.GetFullPath(steamPath);
            using var machine = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
            return machine?.GetValue("InstallPath") as string;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }
}
