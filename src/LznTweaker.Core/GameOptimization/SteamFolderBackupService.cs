using System.Diagnostics;
using System.IO;
using System.Text.Json;
using LznTweaker.Core.Tweaks;

namespace LznTweaker.Core.GameOptimization;

public enum SteamFolderKind
{
    /// <summary>steamapps\shadercache\&lt;AppID&gt; da biblioteca onde o jogo está.</summary>
    ShaderCache,
    /// <summary>userdata\&lt;conta&gt;\730: configurações locais do CS2 (gráficos, binds, convars).</summary>
    UserConfig
}

public sealed record SteamFolderTarget(
    string OperationId,
    SteamFolderKind Kind,
    SupportedGame Game,
    string FolderPath,
    string Label,
    long Bytes,
    int Files);

public sealed record SteamFolderBackup(
    string OperationId,
    SteamFolderKind Kind,
    string GameId,
    string Label,
    string OriginalPath,
    string BackupPath,
    DateTimeOffset CreatedAt);

/// <summary>
/// "Redefine" pastas da Steam renomeando-as para um backup ao lado (nada é apagado), como no vídeo,
/// e restaura devolvendo o backup ao lugar. Exige a Steam e o jogo fechados, porque a Steam recria
/// e sincroniza essas pastas enquanto está aberta.
/// </summary>
public sealed class SteamFolderBackupService
{
    public const string OperationPrefix = "steam-folder:";
    private const string BackupMarker = ".lzntweaker-backup-";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly TweakOperationHistory _history;
    private readonly Func<string, bool> _isProcessRunning;

    public SteamFolderBackupService(TweakOperationHistory history) : this(history, null) { }

    internal SteamFolderBackupService(TweakOperationHistory history, Func<string, bool>? isProcessRunning)
    {
        _history = history;
        _isProcessRunning = isProcessRunning ?? (name => Process.GetProcessesByName(name).Length > 0);
    }

    /// <summary>Pastas que podem ser redefinidas para o jogo. O userdata\730 só existe para o CS2.</summary>
    public IReadOnlyList<SteamFolderTarget> GetTargets(DetectedGame game, string? steamRoot)
    {
        var targets = new List<SteamFolderTarget>();
        if (game.Game.SteamAppId is not { } appId)
            return targets;

        if (game.SteamLibraryPath is { } library)
        {
            var shaderCache = Path.Combine(library, "steamapps", "shadercache", appId);
            if (Directory.Exists(shaderCache))
            {
                var (bytes, files) = ShaderCacheService.Measure(shaderCache, CancellationToken.None);
                targets.Add(new SteamFolderTarget($"{OperationPrefix}shadercache:{appId}", SteamFolderKind.ShaderCache, game.Game,
                    Path.GetFullPath(shaderCache), $"Cache de shaders da Steam ({game.Game.Name})", bytes, files));
            }
        }

        if (game.Game.Id == SupportedGames.Cs2Id && steamRoot is not null)
        {
            foreach (var account in SteamAccounts.List(steamRoot))
            {
                var config = Path.Combine(account.UserDataPath, appId);
                if (!Directory.Exists(config))
                    continue;
                var (bytes, files) = ShaderCacheService.Measure(config, CancellationToken.None);
                var name = account.PersonaName is null ? $"conta {account.AccountId}" : $"{account.PersonaName} ({account.AccountId})";
                targets.Add(new SteamFolderTarget($"{OperationPrefix}userdata:{appId}:{account.AccountId}", SteamFolderKind.UserConfig, game.Game,
                    Path.GetFullPath(config), $"Configurações locais do CS2 — {name}", bytes, files));
            }
        }
        return targets;
    }

    /// <summary>Motivo para não mexer agora, ou null se a Steam e o jogo estão fechados.</summary>
    public string? GetBlockingReason(SupportedGame game) =>
        _isProcessRunning("steam") ? "Feche a Steam por completo (inclusive na bandeja do sistema) antes de redefinir pastas dela."
        : _isProcessRunning(game.ProcessName) ? $"Feche o {game.Name} antes de continuar."
        : null;

    public async Task<TweakOperationResult> BackupAndResetAsync(SteamFolderTarget target, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.Now;
        var requested = $"Redefinir {target.Label}";
        if (GetBlockingReason(target.Game) is { } reason)
            return await RecordAsync(target.OperationId, startedAt, requested, "Não alterado", "Bloqueado", reason + " Nada foi alterado.", null, cancellationToken);
        if (!Directory.Exists(target.FolderPath))
            return await RecordAsync(target.OperationId, startedAt, requested, "Pasta ausente", "Nada a alterar", "A pasta já não existe; nada a redefinir.", null, cancellationToken);

        var backupPath = $"{target.FolderPath}{BackupMarker}{startedAt:yyyyMMdd-HHmmss}";
        try
        {
            Directory.Move(target.FolderPath, backupPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return await RecordAsync(target.OperationId, startedAt, requested, "Pasta original", "Falha ou não verificado",
                $"Não foi possível mover a pasta (algum arquivo pode estar em uso): {ex.Message} Nada foi alterado.", null, cancellationToken);
        }
        if (Directory.Exists(target.FolderPath) || !Directory.Exists(backupPath))
            return await RecordAsync(target.OperationId, startedAt, requested, "Pasta original", "Falha ou não verificado",
                "A pasta não ficou como esperado após a renomeação; confira manualmente.", null, cancellationToken);

        var rollback = JsonSerializer.Serialize(new BackupData(target.Kind, target.Game.Id, target.Label, target.FolderPath, backupPath));
        var message = target.Kind == SteamFolderKind.UserConfig
            ? $"A pasta foi guardada em {backupPath}. O CS2 vai recriar as configurações padrão ao abrir (binds e ajustes pessoais voltam ao padrão)."
            : $"A pasta foi guardada em {backupPath}. A Steam/o jogo recriam o cache; as primeiras partidas podem ter travadas de compilação.";
        return await RecordAsync(target.OperationId, startedAt, requested, $"{target.Files:N0} arquivo(s), {ShaderCacheService.FormatBytes(target.Bytes)}",
            "Sucesso verificado", message, rollback, cancellationToken);
    }

    /// <summary>Backups feitos pelo app que ainda existem no disco e não foram restaurados.</summary>
    public async Task<IReadOnlyList<SteamFolderBackup>> GetBackupsAsync(CancellationToken cancellationToken = default)
    {
        // Cada redefinição gera um backup próprio; restaurar move o backup de volta, então "pendente" = a pasta de backup ainda existe.
        var backups = new Dictionary<string, SteamFolderBackup>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in await _history.ReadAsync(cancellationToken))
        {
            if (!entry.TweakId.StartsWith(OperationPrefix, StringComparison.Ordinal) || entry.Outcome != "Sucesso verificado" || entry.RollbackData is null)
                continue;
            try
            {
                var data = JsonSerializer.Deserialize<BackupData>(entry.RollbackData, JsonOptions);
                if (data is not null && data.BackupPath.Contains(BackupMarker, StringComparison.Ordinal) &&
                    string.Equals(Path.GetDirectoryName(data.BackupPath), Path.GetDirectoryName(data.OriginalPath), StringComparison.OrdinalIgnoreCase))
                    backups[data.BackupPath] = new SteamFolderBackup(entry.TweakId, data.Kind, data.GameId, data.Label, data.OriginalPath, data.BackupPath, entry.StartedAt);
            }
            catch (JsonException) { }
        }
        return backups.Values.Where(backup => Directory.Exists(backup.BackupPath)).OrderByDescending(backup => backup.CreatedAt).ToArray();
    }

    /// <summary>
    /// Devolve o backup ao lugar. Se a Steam/o jogo já recriaram a pasta, ela é guardada ao lado
    /// (".lzntweaker-recriada-…") em vez de apagada.
    /// </summary>
    public async Task<TweakOperationResult> RestoreAsync(SteamFolderBackup backup, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.Now;
        var requested = $"Restaurar {backup.Label}";
        var game = SupportedGames.All.FirstOrDefault(candidate => candidate.Id == backup.GameId);
        if (game is not null && GetBlockingReason(game) is { } reason)
            return await RecordAsync(backup.OperationId, startedAt, requested, "Não alterado", "Bloqueado", reason + " Nada foi alterado.", null, cancellationToken);
        if (!Directory.Exists(backup.BackupPath))
            return await RecordAsync(backup.OperationId, startedAt, requested, "Backup ausente", "Falha ou não verificado", "O backup não existe mais no disco.", null, cancellationToken);

        string? recreatedKept = null;
        try
        {
            if (Directory.Exists(backup.OriginalPath))
            {
                recreatedKept = $"{backup.OriginalPath}.lzntweaker-recriada-{startedAt:yyyyMMdd-HHmmss}";
                Directory.Move(backup.OriginalPath, recreatedKept);
            }
            Directory.Move(backup.BackupPath, backup.OriginalPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return await RecordAsync(backup.OperationId, startedAt, requested, "Backup guardado", "Falha ou não verificado",
                $"Não foi possível restaurar: {ex.Message}", null, cancellationToken);
        }

        var verified = Directory.Exists(backup.OriginalPath) && !Directory.Exists(backup.BackupPath);
        return await RecordAsync(backup.OperationId, startedAt, requested, "Backup guardado",
            verified ? "Reversão verificada" : "Falha ou não verificado",
            verified
                ? "A pasta original voltou ao lugar." + (recreatedKept is null ? string.Empty : $" A versão recriada foi guardada em {recreatedKept}.")
                : "A restauração não pôde ser confirmada; confira as pastas manualmente.",
            null, cancellationToken);
    }

    private async Task<TweakOperationResult> RecordAsync(string operationId, DateTimeOffset startedAt, string requested, string previous, string outcome,
        string message, string? rollback, CancellationToken cancellationToken)
    {
        var result = new TweakOperationResult(Guid.NewGuid(), operationId, startedAt, previous, requested, outcome, message, false,
            rollback is null ? null : "Devolver a pasta de backup ao lugar original",
            rollback is null ? "Não se aplica" : "Disponível enquanto a pasta de backup existir.", rollback);
        await _history.AppendAsync(result, cancellationToken);
        return result;
    }

    private sealed record BackupData(SteamFolderKind Kind, string GameId, string Label, string OriginalPath, string BackupPath);
}
