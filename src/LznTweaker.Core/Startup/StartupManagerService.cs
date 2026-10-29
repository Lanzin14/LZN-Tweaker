using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace LznTweaker.Core.Startup;

public sealed class StartupManagerService
{
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Wow64RunSubKey = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupApprovedRunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string StartupApprovedRun32SubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
    private const string StartupApprovedFolderSubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    private static readonly byte[] EnabledBinary = [0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
    private static readonly byte[] DisabledBinary = [0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

    public Task<IReadOnlyList<StartupItem>> GetStartupItemsAsync(CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<StartupItem>>(() => GetStartupItems(cancellationToken), cancellationToken);

    private IReadOnlyList<StartupItem> GetStartupItems(CancellationToken cancellationToken)
    {
        var items = new List<StartupItem>();

        // 1. HKCU\Software\Microsoft\Windows\CurrentVersion\Run
        ReadRegistryRun(Registry.CurrentUser, RunSubKey, StartupApprovedRunSubKey, StartupLocation.CurrentUserRegistry, items, cancellationToken);

        // 2. HKLM\Software\Microsoft\Windows\CurrentVersion\Run
        ReadRegistryRun(Registry.LocalMachine, RunSubKey, StartupApprovedRunSubKey, StartupLocation.LocalMachineRegistry, items, cancellationToken);

        // 3. HKLM 32-bit (WOW6432Node): o Windows guarda o estado desses itens em StartupApproved\Run32
        if (Environment.Is64BitOperatingSystem)
        {
            ReadRegistryRun(Registry.LocalMachine, Wow64RunSubKey, StartupApprovedRun32SubKey, StartupLocation.LocalMachineRegistry, items, cancellationToken);
        }

        // 4. Pasta Inicializar do Usuário (estado em HKCU)
        var userStartupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        ReadFolderStartup(userStartupPath, StartupLocation.UserStartupFolder, approvedInLocalMachine: false, items, cancellationToken);

        // 5. Pasta Inicializar de Todos os Usuários (estado em HKLM)
        var commonStartupPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
        ReadFolderStartup(commonStartupPath, StartupLocation.CommonStartupFolder, approvedInLocalMachine: true, items, cancellationToken);

        return items.OrderByDescending(i => i.IsEnabled).ThenBy(i => i.Name).ToArray();
    }

    private static void ReadRegistryRun(
        RegistryKey rootKey,
        string subKeyPath,
        string approvedSubKey,
        StartupLocation location,
        List<StartupItem> items,
        CancellationToken cancellationToken)
    {
        try
        {
            using var key = rootKey.OpenSubKey(subKeyPath, writable: false);
            if (key is null) return;

            var approvedInLocalMachine = rootKey == Registry.LocalMachine;
            foreach (var valueName in key.GetValueNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var command = Convert.ToString(key.GetValue(valueName));
                if (string.IsNullOrWhiteSpace(command)) continue;

                var (publisher, impact, category, cleanName) = AnalyzeCommand(valueName, command);

                items.Add(new StartupItem
                {
                    Id = $"{location}_{approvedSubKey}_{valueName}",
                    Name = cleanName,
                    Command = command,
                    ExecutablePath = ExtractExecutablePath(command),
                    Location = location,
                    LocationKeyName = valueName,
                    ApprovedSubKey = approvedSubKey,
                    ApprovedInLocalMachine = approvedInLocalMachine,
                    Category = category,
                    Publisher = publisher,
                    Impact = impact,
                    IsEnabled = IsApproved(approvedInLocalMachine, approvedSubKey, valueName)
                });
            }
        }
        catch (Exception)
        {
            // Erros de leitura ou chave ausente são ignorados com segurança
        }
    }

    private static void ReadFolderStartup(
        string folderPath,
        StartupLocation location,
        bool approvedInLocalMachine,
        List<StartupItem> items,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folderPath)) return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileName = Path.GetFileName(file);
                if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;

                var (publisher, impact, category, cleanName) = AnalyzeCommand(Path.GetFileNameWithoutExtension(file), file);

                items.Add(new StartupItem
                {
                    Id = $"{location}_{fileName}",
                    Name = cleanName,
                    Command = file,
                    ExecutablePath = file,
                    Location = location,
                    LocationKeyName = fileName,
                    ApprovedSubKey = StartupApprovedFolderSubKey,
                    ApprovedInLocalMachine = approvedInLocalMachine,
                    Category = category,
                    Publisher = publisher,
                    Impact = impact,
                    IsEnabled = IsApproved(approvedInLocalMachine, StartupApprovedFolderSubKey, fileName)
                });
            }
        }
        catch
        {
            // Pasta protegida ou vazia
        }
    }

    /// <summary>Grava o estado no mesmo local usado pelo Gerenciador de Tarefas e confirma a leitura.</summary>
    public bool SetItemState(StartupItem item, bool enabled)
    {
        try
        {
            var root = item.ApprovedInLocalMachine ? Registry.LocalMachine : Registry.CurrentUser;
            using (var approvedKey = root.CreateSubKey(item.ApprovedSubKey))
            {
                if (approvedKey is null)
                    return false;
                approvedKey.SetValue(item.LocationKeyName, enabled ? EnabledBinary : DisabledBinary, RegistryValueKind.Binary);
            }

            if (IsApproved(item.ApprovedInLocalMachine, item.ApprovedSubKey, item.LocationKeyName) != enabled)
                return false;
            item.IsEnabled = enabled;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsApproved(bool inLocalMachine, string approvedSubKey, string valueName)
    {
        try
        {
            var root = inLocalMachine ? Registry.LocalMachine : Registry.CurrentUser;
            using var key = root.OpenSubKey(approvedSubKey, writable: false);
            if (key?.GetValue(valueName) is byte[] bytes && bytes.Length > 0)
            {
                // No padrão do Windows, 0x02/0x06 = Ativado; bit 0 ligado (0x03/0x07) = Desativado
                return (bytes[0] & 1) == 0;
            }
        }
        catch { }

        // Por padrão no Windows, se não houver entrada no StartupApproved, o item está ATIVADO
        return true;
    }

    private static (string Publisher, string Impact, StartupCategory Category, string CleanName) AnalyzeCommand(string rawName, string command)
    {
        var lower = (rawName + " " + command).ToLowerInvariant();

        // Launchers de Jogos
        if (lower.Contains("steam"))
            return ("Valve Corporation", "Alto", StartupCategory.GameLauncher, rawName);
        if (lower.Contains("discord"))
            return ("Discord Inc.", "Alto", StartupCategory.CommunicationAndChat, rawName);
        if (lower.Contains("epicgames") || lower.Contains("epic games"))
            return ("Epic Games Inc.", "Alto", StartupCategory.GameLauncher, rawName);
        if (lower.Contains("riot") || lower.Contains("vanguard"))
            return ("Riot Games", "Alto", StartupCategory.GameLauncher, rawName);
        if (lower.Contains("spotify"))
            return ("Spotify AB", "Médio", StartupCategory.MediaAndAudio, rawName);
        if (lower.Contains("battle.net") || lower.Contains("battlenet"))
            return ("Blizzard Entertainment", "Alto", StartupCategory.GameLauncher, rawName);
        if (lower.Contains("origin.exe") || lower.Contains("eadesktop") || lower.Contains("ea app"))
            return ("Electronic Arts", "Alto", StartupCategory.GameLauncher, rawName);
        if (lower.Contains("ubisoft") || lower.Contains("uplay"))
            return ("Ubisoft", "Médio", StartupCategory.GameLauncher, rawName);

        // Periféricos e Hardware
        if (lower.Contains("lghub") || lower.Contains("logitech"))
            return ("Logitech", "Médio", StartupCategory.HardwareAndPeripherals, rawName);
        if (lower.Contains("razer") || lower.Contains("synapse"))
            return ("Razer Inc.", "Alto", StartupCategory.HardwareAndPeripherals, rawName);
        if (lower.Contains("icue") || lower.Contains("corsair"))
            return ("Corsair", "Alto", StartupCategory.HardwareAndPeripherals, rawName);
        if (lower.Contains("steelseries"))
            return ("SteelSeries", "Médio", StartupCategory.HardwareAndPeripherals, rawName);

        // Nuvem e Sincronização
        if (lower.Contains("onedrive"))
            return ("Microsoft Corporation", "Médio", StartupCategory.CloudAndSync, rawName);
        if (lower.Contains("googledrive") || lower.Contains("google drive"))
            return ("Google LLC", "Médio", StartupCategory.CloudAndSync, rawName);

        // Navegadores e Utilitários comuns
        if (lower.Contains("chrome"))
            return ("Google LLC", "Médio", StartupCategory.SystemUtility, rawName);
        if (lower.Contains("msedge"))
            return ("Microsoft Corporation", "Médio", StartupCategory.SystemUtility, rawName);

        // Tentar obter dados do arquivo executável real
        var exePath = ExtractExecutablePath(command);
        if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
        {
            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                var company = string.IsNullOrWhiteSpace(versionInfo.CompanyName) ? "Fabricante não informado" : versionInfo.CompanyName;
                var desc = string.IsNullOrWhiteSpace(versionInfo.FileDescription) ? rawName : versionInfo.FileDescription;
                return (company, "Baixo", StartupCategory.SystemUtility, desc);
            }
            catch { }
        }

        return ("Não identificado", "Baixo", StartupCategory.SystemUtility, rawName);
    }

    private static string? ExtractExecutablePath(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;

        var trimmed = command.Trim();
        if (trimmed.StartsWith("\""))
        {
            var nextQuote = trimmed.IndexOf('\"', 1);
            if (nextQuote > 1)
                return trimmed[1..nextQuote];
        }

        var spaceIndex = trimmed.IndexOf(' ');
        return spaceIndex > 0 ? trimmed[..spaceIndex] : trimmed;
    }
}
