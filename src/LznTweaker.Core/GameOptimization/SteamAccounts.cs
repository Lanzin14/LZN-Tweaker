using System.IO;
using LznTweaker.Core.GameTuning;

namespace LznTweaker.Core.GameOptimization;

/// <summary>Conta Steam local: o AccountId é o nome da pasta em userdata (SteamID64 − 76561197960265728).</summary>
public sealed record SteamAccount(string AccountId, string? PersonaName, bool MostRecent, string UserDataPath);

public static class SteamAccounts
{
    private const ulong SteamId64Base = 76561197960265728;

    /// <summary>Pastas de userdata com nome amigável quando a conta aparece em config\loginusers.vdf.</summary>
    public static IReadOnlyList<SteamAccount> List(string steamRoot)
    {
        var userData = Path.Combine(steamRoot, "userdata");
        if (!Directory.Exists(userData))
            return [];

        var known = ReadLoginUsers(Path.Combine(steamRoot, "config", "loginusers.vdf"));
        return Directory.EnumerateDirectories(userData)
            .Select(directory => (Path: directory, Id: Path.GetFileName(directory)))
            .Where(entry => ulong.TryParse(entry.Id, out var id) && id > 0)
            .Select(entry => known.TryGetValue(entry.Id, out var login)
                ? new SteamAccount(entry.Id, login.Persona, login.MostRecent, entry.Path)
                : new SteamAccount(entry.Id, null, false, entry.Path))
            .OrderByDescending(account => account.MostRecent)
            .ThenBy(account => account.PersonaName is null)
            .ToArray();
    }

    internal static Dictionary<string, (string? Persona, bool MostRecent)> ReadLoginUsers(string path)
    {
        var accounts = new Dictionary<string, (string?, bool)>(StringComparer.Ordinal);
        if (!File.Exists(path))
            return accounts;
        try
        {
            var users = KeyValuesReader.Parse(File.ReadAllText(path))
                .FirstOrDefault(node => node.Key.Equals("users", StringComparison.OrdinalIgnoreCase));
            foreach (var user in users?.Children ?? [])
            {
                if (!ulong.TryParse(user.Key, out var steamId64) || steamId64 <= SteamId64Base)
                    continue;
                accounts[(steamId64 - SteamId64Base).ToString()] = (user.ChildValue("PersonaName"), user.ChildValue("MostRecent") == "1");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sem nomes amigáveis; as pastas continuam listadas pelo número.
        }
        return accounts;
    }
}
