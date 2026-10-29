using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LznTweaker.Core.Tweaks;

namespace LznTweaker.Core.GameTuning;

public sealed record DeadlockInstallation(string RootPath, string GameInfoPath, bool DetectedAutomatically);

public enum GameConfigState
{
    /// <summary>O arquivo atual é o do usuário/jogo; nada aplicado pelo app.</summary>
    NotApplied,
    /// <summary>O arquivo atual é exatamente o perfil aplicado pelo app.</summary>
    Applied,
    /// <summary>O app aplicou um perfil, mas o arquivo mudou depois (normalmente uma atualização do jogo).</summary>
    ChangedOutsideApp
}

public sealed record GameConfigStatus(GameConfigState State, string? AppliedProfileId, DateTimeOffset? AppliedAt, string Message);

/// <summary>Perfil baixado e validado, pronto para ser aplicado.</summary>
public sealed record ProfileDownload(
    GameConfigProfile Profile,
    byte[] Content,
    string Sha256,
    string? CommitSha,
    DateTimeOffset? CommitDate,
    IReadOnlyList<string> CompatibilityWarnings);

/// <summary>
/// Aplica perfis de configuração de jogos substituindo arquivos locais da engine, com backup do original,
/// verificação por hash, registro no histórico e Undo protegido contra restaurar arquivos desatualizados.
/// Primeiro módulo: Deadlock (game/citadel/gameinfo.gi).
/// </summary>
public sealed class GameTuningService
{
    public const string DeadlockOperationId = "deadlock-gameinfo";
    private const string StateFileName = "state.json";
    private const string BackupFileName = "gameinfo.gi.original";
    private const int MinimumProfileBytes = 1_000;
    private const int MaximumProfileBytes = 2_000_000;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly HttpClient _http;
    private readonly TweakOperationHistory _history;
    private readonly SteamLibraryLocator _locator;
    private readonly string _stateDirectory;
    private readonly Func<bool> _isDeadlockRunning;

    public GameTuningService(HttpClient http, TweakOperationHistory history, SteamLibraryLocator locator)
        : this(http, history, locator, stateDirectory: null, isDeadlockRunning: null) { }

    internal GameTuningService(HttpClient http, TweakOperationHistory history, SteamLibraryLocator locator,
        string? stateDirectory, Func<bool>? isDeadlockRunning)
    {
        _http = http;
        _history = history;
        _locator = locator;
        _stateDirectory = stateDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LznTweaker", "GameTuning", "Deadlock");
        _isDeadlockRunning = isDeadlockRunning ?? (() => Process.GetProcessesByName("deadlock").Length > 0);
    }

    /// <summary>Procura o Deadlock nas bibliotecas da Steam.</summary>
    public DeadlockInstallation? LocateDeadlock()
    {
        var root = _locator.FindAppInstallPath(DeadlockProfiles.SteamAppId);
        return root is null ? null : FromRoot(root, detected: true);
    }

    /// <summary>Aceita a pasta do Deadlock, a pasta game\citadel ou o próprio gameinfo.gi escolhidos pelo usuário.</summary>
    public DeadlockInstallation? FromUserSelection(string path, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Nenhuma pasta foi escolhida.";
            return null;
        }

        var full = Path.GetFullPath(path);
        // Pasta do jogo, game\ ou game\citadel\.
        var candidates = new[]
        {
            full,
            Path.Combine(full, ".."),
            Path.Combine(full, "..", "..")
        };
        if (File.Exists(full) && Path.GetFileName(full).Equals("gameinfo.gi", StringComparison.OrdinalIgnoreCase))
            candidates = [Path.Combine(Path.GetDirectoryName(full)!, "..", "..")];
        foreach (var candidate in candidates)
        {
            var installation = FromRoot(Path.GetFullPath(candidate), detected: false);
            if (installation is not null)
                return installation;
        }

        error = "A pasta escolhida não contém game\\citadel\\gameinfo.gi. Selecione a pasta do Deadlock (normalmente steamapps\\common\\Deadlock).";
        return null;
    }

    public GameConfigStatus GetStatus(DeadlockInstallation installation)
    {
        var state = LoadState(installation);
        if (state is null)
            return new GameConfigStatus(GameConfigState.NotApplied, null, null, "O gameinfo.gi atual é o do jogo (ou seu); nenhum perfil do app está aplicado.");

        var profileName = DeadlockProfiles.All.FirstOrDefault(profile => profile.Id == state.ProfileId)?.Name ?? state.ProfileId;
        return HashFile(installation.GameInfoPath) == state.AppliedSha256
            ? new GameConfigStatus(GameConfigState.Applied, state.ProfileId, state.AppliedAt,
                $"Perfil \"{profileName}\" aplicado em {state.AppliedAt.LocalDateTime:g}. O original está guardado e pode ser restaurado.")
            : new GameConfigStatus(GameConfigState.ChangedOutsideApp, state.ProfileId, state.AppliedAt,
                $"O perfil \"{profileName}\" foi substituído depois de aplicado — normalmente por uma atualização do Deadlock. Reaplique um perfil se quiser voltar a usá-lo.");
    }

    /// <summary>Baixa o perfil do repositório do autor, valida o conteúdo e compara com o arquivo atual.</summary>
    public async Task<ProfileDownload> DownloadAsync(GameConfigProfile profile, DeadlockInstallation installation, CancellationToken cancellationToken = default)
    {
        var url = DeadlockProfiles.RawBaseUrl + EscapePath(profile.RepositoryPath);
        byte[] content;
        try
        {
            content = await _http.GetByteArrayAsync(url, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Não foi possível baixar o perfil do GitHub ({ex.Message}). Verifique a conexão.", ex);
        }

        var text = ValidateProfile(content);
        var current = File.ReadAllText(installation.GameInfoPath);
        var (commitSha, commitDate) = await TryGetLatestCommitAsync(profile.RepositoryPath, cancellationToken);
        return new ProfileDownload(profile, content, Sha256(content), commitSha, commitDate, CompareSearchPaths(current, text));
    }

    public async Task<TweakOperationResult> ApplyAsync(DeadlockInstallation installation, ProfileDownload download, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.Now;
        var profile = download.Profile;
        if (_isDeadlockRunning())
            return await RecordAsync(startedAt, profile.Name, "Não alterado", "Bloqueado", "Feche o Deadlock antes de aplicar um perfil; nada foi alterado.", cancellationToken);

        var target = installation.GameInfoPath;
        var currentHash = HashFile(target);
        if (currentHash == download.Sha256)
            return await RecordAsync(startedAt, profile.Name, "Perfil já presente", "Nada a alterar", "O gameinfo.gi atual já é idêntico a este perfil.", cancellationToken);

        Directory.CreateDirectory(_stateDirectory);
        var backupPath = Path.Combine(_stateDirectory, BackupFileName);
        var previous = LoadState(installation);
        string backupHash;
        string backupNote;
        if (previous is not null && previous.AppliedSha256 == currentHash && File.Exists(backupPath) && HashFile(backupPath) == previous.BackupSha256)
        {
            // Trocar de um perfil do app para outro mantém o original de antes do primeiro perfil.
            backupHash = previous.BackupSha256;
            backupNote = "O backup original existente foi mantido.";
        }
        else
        {
            WriteVerified(backupPath, File.ReadAllBytes(target), currentHash);
            backupHash = currentHash;
            backupNote = previous is null
                ? "Backup do arquivo atual criado."
                : "O arquivo mudou desde o último perfil (provavelmente uma atualização do jogo); o backup foi renovado com a versão atual.";
        }

        try
        {
            WriteVerified(target, download.Content, download.Sha256);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            var restored = TryRestore(target, backupPath, backupHash);
            return await RecordAsync(startedAt, profile.Name, "Arquivo original", "Falha ou não verificado",
                $"Não foi possível gravar o perfil: {ex.Message} {(restored ? "O arquivo original foi mantido." : "ATENÇÃO: confira o gameinfo.gi; use 'Verificar integridade dos arquivos' na Steam se o jogo não abrir.")}",
                cancellationToken);
        }

        SaveState(new AppliedState(Path.GetFullPath(target), profile.Id, download.Sha256, backupHash, startedAt,
            DeadlockProfiles.RawBaseUrl + EscapePath(profile.RepositoryPath), download.CommitSha));
        var source = download.CommitDate is { } date ? $" Versão do GitHub de {date.LocalDateTime:d} ({download.CommitSha?[..7]})." : string.Empty;
        return await RecordAsync(startedAt, profile.Name, $"gameinfo.gi anterior (SHA-256 {backupHash[..12]}…)", "Sucesso verificado",
            $"Perfil gravado e conferido por hash.{source} {backupNote} O Deadlock substitui este arquivo a cada atualização grande.",
            cancellationToken);
    }

    /// <summary>
    /// Restaura o original somente se o arquivo atual ainda é exatamente o perfil aplicado. Se o jogo já o substituiu,
    /// restaurar o backup antigo colocaria um arquivo desatualizado no jogo, então o Undo é recusado.
    /// </summary>
    public async Task<TweakOperationResult> UndoAsync(DeadlockInstallation installation, CancellationToken cancellationToken = default)
    {
        const string requested = "Desfazer (Deadlock)";
        var startedAt = DateTimeOffset.Now;
        var state = LoadState(installation);
        if (state is null)
            return await RecordAsync(startedAt, requested, "Sem perfil aplicado", "Nada a alterar", "Nenhum perfil aplicado pelo app neste gameinfo.gi.", cancellationToken);
        if (_isDeadlockRunning())
            return await RecordAsync(startedAt, requested, "Não alterado", "Bloqueado", "Feche o Deadlock antes de desfazer; nada foi alterado.", cancellationToken);

        var target = installation.GameInfoPath;
        var backupPath = Path.Combine(_stateDirectory, BackupFileName);
        if (HashFile(target) != state.AppliedSha256)
        {
            var kept = ArchiveObsoleteBackup(backupPath);
            DeleteState();
            return await RecordAsync(startedAt, requested, "Arquivo alterado fora do app", "Reversão recusada",
                "O gameinfo.gi mudou depois que o perfil foi aplicado (normalmente uma atualização do Deadlock, que já instala o arquivo do jogo). " +
                "O backup antigo não foi restaurado para não colocar um arquivo desatualizado no jogo." +
                (kept is null ? string.Empty : $" Ele foi guardado em {kept}."), cancellationToken);
        }
        if (!File.Exists(backupPath) || HashFile(backupPath) != state.BackupSha256)
            return await RecordAsync(startedAt, requested, "Perfil aplicado", "Falha ou não verificado",
                "O backup original está ausente ou foi alterado; nada foi restaurado. Use 'Verificar integridade dos arquivos' na Steam para recuperar o arquivo do jogo.",
                cancellationToken);

        try
        {
            WriteVerified(target, File.ReadAllBytes(backupPath), state.BackupSha256);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return await RecordAsync(startedAt, requested, "Perfil aplicado", "Falha ou não verificado", $"Não foi possível restaurar o original: {ex.Message}", cancellationToken);
        }

        File.Delete(backupPath);
        DeleteState();
        return await RecordAsync(startedAt, requested, "Perfil aplicado", "Reversão verificada",
            "O gameinfo.gi original foi restaurado e conferido por hash; o perfil modificado foi descartado.", cancellationToken);
    }

    /// <summary>Confere se o conteúdo baixado é um gameinfo.gi completo (e não uma página de erro ou um arquivo truncado).</summary>
    internal static string ValidateProfile(byte[] content)
    {
        if (content.Length is < MinimumProfileBytes or > MaximumProfileBytes)
            throw new InvalidDataException($"O arquivo baixado tem {content.Length:N0} bytes, fora do tamanho esperado para um gameinfo.gi.");
        var text = Encoding.UTF8.GetString(content);
        if (text.TrimStart('﻿', ' ', '\r', '\n', '\t').StartsWith('<'))
            throw new InvalidDataException("O GitHub retornou uma página HTML em vez do arquivo de configuração.");
        var searchPaths = GetSearchPaths(text);
        if (searchPaths.Count == 0 || !searchPaths.Any(entry => entry.Contains("citadel", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("O arquivo baixado não tem a estrutura GameInfo/FileSystem/SearchPaths do Deadlock.");
        return text;
    }

    /// <summary>Caminhos de conteúdo do arquivo atual que o perfil deixaria de carregar.</summary>
    internal static IReadOnlyList<string> CompareSearchPaths(string currentText, string profileText)
    {
        var current = GetSearchPaths(currentText);
        if (current.Count == 0)
            return [];
        var profile = GetSearchPaths(profileText).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return current.Where(entry => !profile.Contains(entry))
            .Select(entry => entry.Contains("dmm_", StringComparison.OrdinalIgnoreCase)
                ? $"O perfil não carrega \"{entry}\", adicionado pelo Deadlock Mod Manager: mods instalados por ele deixam de carregar (e o gerenciador pode reverter o arquivo)."
                : $"O perfil não carrega \"{entry}\", presente no seu gameinfo.gi atual. Pode ser conteúdo novo do jogo (perfil desatualizado para este patch) ou de um gerenciador de mods.")
            .ToArray();
    }

    private static List<string> GetSearchPaths(string text)
    {
        try
        {
            var gameInfo = KeyValuesReader.Parse(text).FirstOrDefault(node => node.Key.Equals("GameInfo", StringComparison.OrdinalIgnoreCase));
            var searchPaths = gameInfo?.Child("FileSystem")?.Child("SearchPaths");
            return searchPaths?.Children.Where(entry => entry.Value is { Length: > 0 })
                .Select(entry => $"{entry.Key} {entry.Value}").Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException)
        {
            return [];
        }
    }

    private async Task<(string? Sha, DateTimeOffset? Date)> TryGetLatestCommitAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{DeadlockProfiles.CommitsApiUrl}?path={Uri.EscapeDataString(repositoryPath)}&per_page=1";
            using var document = JsonDocument.Parse(await _http.GetStringAsync(url, cancellationToken));
            var commit = document.RootElement[0];
            return (commit.GetProperty("sha").GetString(),
                commit.GetProperty("commit").GetProperty("committer").GetProperty("date").GetDateTimeOffset());
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException or FormatException)
        {
            // A data da versão é informativa; sem ela (limite da API, offline), o perfil ainda é validado pelo conteúdo.
            return (null, null);
        }
    }

    /// <summary>Grava em arquivo temporário na mesma pasta, troca de forma atômica e confere o hash final.</summary>
    private static void WriteVerified(string path, byte[] content, string expectedSha256)
    {
        var directory = Path.GetDirectoryName(path)!;
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.lzntweaker-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temporary, content);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
        if (HashFile(path) != expectedSha256)
            throw new InvalidDataException($"A verificação por hash de {Path.GetFileName(path)} falhou após a gravação.");
    }

    private static bool TryRestore(string target, string backupPath, string backupHash)
    {
        try
        {
            if (File.Exists(target) && HashFile(target) == backupHash)
                return true;
            WriteVerified(target, File.ReadAllBytes(backupPath), backupHash);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return false;
        }
    }

    private string? ArchiveObsoleteBackup(string backupPath)
    {
        if (!File.Exists(backupPath))
            return null;
        var archived = Path.Combine(_stateDirectory, $"gameinfo.gi.original-{DateTime.Now:yyyyMMdd-HHmmss}");
        File.Move(backupPath, archived);
        return archived;
    }

    private DeadlockInstallation? FromRoot(string root, bool detected)
    {
        var gameInfo = Path.Combine(root, "game", "citadel", "gameinfo.gi");
        return File.Exists(gameInfo) ? new DeadlockInstallation(Path.GetFullPath(root), Path.GetFullPath(gameInfo), detected) : null;
    }

    private AppliedState? LoadState(DeadlockInstallation installation)
    {
        var path = Path.Combine(_stateDirectory, StateFileName);
        if (!File.Exists(path))
            return null;
        try
        {
            var state = JsonSerializer.Deserialize<AppliedState>(File.ReadAllText(path));
            return state is not null && string.Equals(state.TargetPath, installation.GameInfoPath, StringComparison.OrdinalIgnoreCase) ? state : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void SaveState(AppliedState state) =>
        File.WriteAllText(Path.Combine(_stateDirectory, StateFileName), JsonSerializer.Serialize(state, JsonOptions));

    private void DeleteState()
    {
        var path = Path.Combine(_stateDirectory, StateFileName);
        if (File.Exists(path))
            File.Delete(path);
    }

    private async Task<TweakOperationResult> RecordAsync(DateTimeOffset startedAt, string requested, string previousState, string outcome, string message, CancellationToken cancellationToken)
    {
        var result = new TweakOperationResult(Guid.NewGuid(), DeadlockOperationId, startedAt, previousState, requested, outcome, message,
            RequiresRestart: false,
            RollbackMethod: outcome == "Sucesso verificado" ? "Restaurar o gameinfo.gi original guardado pelo app" : null,
            RollbackOutcome: outcome == "Sucesso verificado"
                ? "Disponível enquanto o arquivo não for substituído por uma atualização do jogo."
                : "Não se aplica");
        await _history.AppendAsync(result, cancellationToken);
        return result;
    }

    internal static string EscapePath(string repositoryPath) => Uri.EscapeDataString(repositoryPath).Replace("%2F", "/");

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string Sha256(byte[] content) => Convert.ToHexString(SHA256.HashData(content));

    private sealed record AppliedState(
        string TargetPath,
        string ProfileId,
        string AppliedSha256,
        string BackupSha256,
        DateTimeOffset AppliedAt,
        string SourceUrl,
        string? CommitSha);
}
