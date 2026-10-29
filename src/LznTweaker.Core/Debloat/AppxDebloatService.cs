using System.Text.Json;
using System.Text.RegularExpressions;
using LznTweaker.Core.Execution;
using LznTweaker.Core.Tweaks;

namespace LznTweaker.Core.Debloat;

public sealed record AppxPackageInfo(
    string Name,
    string FriendlyName,
    string PackageFullName,
    string PackageFamilyName,
    string Publisher,
    string Version,
    AppxCategory Category,
    string? Note,
    bool? IsProvisioned);

public sealed record AppxInventory(
    IReadOnlyList<AppxPackageInfo> Packages,
    int ProtectedCount,
    bool ProvisionedKnown,
    /// <summary>Preenchido quando o app foi elevado com uma conta diferente da que usa a área de trabalho.</summary>
    string? AccountWarning);

/// <summary>App removido pelo LznTweaker, lido do histórico, com o caminho de restauração disponível.</summary>
public sealed record RemovedAppxPackage(
    string Name,
    string FriendlyName,
    string PackageFamilyName,
    DateTimeOffset RemovedAt,
    bool RemovedForAllUsers,
    bool FilesKeptAfterRemoval,
    string RollbackData)
{
    /// <summary>Página do app na Microsoft Store, usada quando o re-registro local não é possível.</summary>
    public string StoreUri => AppxDebloatService.GetStoreUri(PackageFamilyName);
}

public sealed record AppxBatchProgress(int Completed, int Total, string Name, string Outcome);

/// <summary>
/// Remove e restaura apps Appx. Toda remoção passa pelo lote coordenado (BatchEngine.ps1) com o ponto de
/// restauração obrigatório, é conferida por consulta posterior e fica no histórico com os dados para restaurar:
/// re-registro do manifesto local quando os arquivos ainda existem; caso contrário, a página do app na Store.
/// </summary>
public sealed partial class AppxDebloatService(ScriptRunnerService scriptRunner, TweakOperationHistory history)
{
    public const string OperationPrefix = "appx:";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<AppxInventory> GetInventoryAsync(CancellationToken cancellationToken = default)
    {
        var run = await scriptRunner.RunAsync(
            new ScriptRunRequest("appxlist", EmbeddedScripts.AppxInventory, "{}", TimeSpan.FromMinutes(2), RequiresElevation: true),
            cancellationToken: cancellationToken);
        if (run.ResultJson is null)
            throw new InvalidOperationException($"Não foi possível listar os aplicativos: {run.ErrorOutput ?? run.Message}");
        return ParseInventory(run.ResultJson);
    }

    internal static AppxInventory ParseInventory(string json)
    {
        var raw = JsonSerializer.Deserialize<RawInventory>(json, JsonOptions)
                  ?? throw new InvalidOperationException("O inventário de aplicativos não pôde ser interpretado.");
        var protectedCount = 0;
        var packages = new List<AppxPackageInfo>();
        // Pacotes multiarquitetura aparecem uma vez por arquitetura; a remoção trata todos pelo nome.
        foreach (var group in (raw.Packages ?? []).GroupBy(package => package.Name, StringComparer.OrdinalIgnoreCase))
        {
            var package = group.First();
            if (AppxCatalog.IsProtected(package.Name, package.SignatureKind ?? string.Empty, package.IsFramework, package.IsResourcePackage, package.NonRemovable))
            {
                protectedCount++;
                continue;
            }
            var entry = AppxCatalog.Classify(package.Name, package.Publisher ?? string.Empty);
            packages.Add(new AppxPackageInfo(package.Name, entry.FriendlyName, package.PackageFullName ?? string.Empty,
                package.PackageFamilyName ?? string.Empty, package.Publisher ?? string.Empty, package.Version ?? string.Empty,
                entry.Category, entry.Note, package.IsProvisioned));
        }

        var accountWarning = raw.InteractiveUserSid is { Length: > 0 } interactive && raw.CurrentUserSid is { Length: > 0 } current &&
                             !string.Equals(interactive, current, StringComparison.OrdinalIgnoreCase)
            ? "O LznTweaker foi elevado com uma conta diferente da que está usando a área de trabalho. A lista e as remoções " +
              "valem para a conta de administrador, não para a sua. Entre como administrador ou use \"Remover para todos os usuários\"."
            : null;
        return new AppxInventory(packages.OrderBy(package => package.FriendlyName, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            protectedCount, raw.ProvisionedKnown, accountWarning);
    }

    /// <summary>Remove os apps em um lote que começa pelo ponto de restauração obrigatório.</summary>
    public async Task<IReadOnlyList<TweakOperationResult>> RemoveAsync(
        IReadOnlyList<AppxPackageInfo> packages,
        bool allUsers,
        IProgress<AppxBatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (packages.Count == 0)
            return [];
        var startedAt = DateTimeOffset.Now;
        var restorePoint = TweakCatalog.GetById(TweakCatalog.CreateRestorePointId);
        var operations = new List<object> { new { TweakId = restorePoint.Id, restorePoint.Name, Kind = "RestorePoint" } };
        operations.AddRange(packages.Select(package => (object)new
        {
            TweakId = OperationPrefix + package.Name, Name = package.FriendlyName, Kind = "RemoveAppx",
            PackageName = package.Name, AllUsers = allUsers
        }));
        var labels = new Dictionary<string, string>(StringComparer.Ordinal) { [restorePoint.Id] = restorePoint.Name };
        foreach (var package in packages)
            labels[OperationPrefix + package.Name] = $"Remover {package.FriendlyName}";

        return await RunBatchAsync("appxremove", operations, requireRestorePoint: true, labels, startedAt, progress, cancellationToken);
    }

    /// <summary>Apps removidos pelo LznTweaker que ainda não foram restaurados, a partir do histórico.</summary>
    public async Task<IReadOnlyList<RemovedAppxPackage>> GetRestorableAsync(CancellationToken cancellationToken = default)
    {
        var pending = new Dictionary<string, RemovedAppxPackage>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in await history.ReadAsync(cancellationToken))
        {
            if (!entry.TweakId.StartsWith(OperationPrefix, StringComparison.Ordinal))
                continue;
            var name = entry.TweakId[OperationPrefix.Length..];
            if (entry.RequestedState.StartsWith("Restaurar", StringComparison.Ordinal))
            {
                if (entry.Outcome is "Reversão verificada" or "Nada a alterar")
                    pending.Remove(name);
                continue;
            }
            if (entry.Outcome == "Sucesso verificado" && TryReadRemoval(entry, out var removed))
                pending[name] = removed;
        }
        return pending.Values.OrderByDescending(item => item.RemovedAt).ToArray();
    }

    /// <summary>Re-registra os apps cujos arquivos ainda existem; os demais voltam como "Requer Microsoft Store".</summary>
    public async Task<IReadOnlyList<TweakOperationResult>> RestoreAsync(
        IReadOnlyList<RemovedAppxPackage> packages,
        IProgress<AppxBatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (packages.Count == 0)
            return [];
        var operations = packages.Select(package => (object)new
        {
            TweakId = OperationPrefix + package.Name, Name = package.FriendlyName, Kind = "RestoreAppx", package.RollbackData
        }).ToList();
        var labels = packages.ToDictionary(package => OperationPrefix + package.Name, package => $"Restaurar {package.FriendlyName}", StringComparer.Ordinal);
        // Como o Undo dos tweaks, restaurar não exige um novo ponto: uma falha da Proteção do Sistema não pode impedir a volta.
        return await RunBatchAsync("appxrestore", operations, requireRestorePoint: false, labels, DateTimeOffset.Now, progress, cancellationToken);
    }

    /// <summary>Link ms-windows-store para a família do pacote; null se o nome não tiver o formato esperado.</summary>
    public static string GetStoreUri(string packageFamilyName) =>
        PackageFamilyNamePattern().IsMatch(packageFamilyName)
            ? $"ms-windows-store://pdp/?PFN={packageFamilyName}"
            : "ms-windows-store://home";

    [GeneratedRegex(@"^[A-Za-z0-9.\-]{3,}_[a-z0-9]{13}$")]
    private static partial Regex PackageFamilyNamePattern();

    private async Task<IReadOnlyList<TweakOperationResult>> RunBatchAsync(
        string name, IReadOnlyList<object> operations, bool requireRestorePoint, IReadOnlyDictionary<string, string> labels,
        DateTimeOffset startedAt, IProgress<AppxBatchProgress>? progress, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            Operations = operations,
            UserSid = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value ?? string.Empty,
            RequireRestorePoint = requireRestorePoint
        });
        var ids = labels.Keys.ToArray();
        var reported = 0;
        var run = await scriptRunner.RunAsync(
            new ScriptRunRequest(name, EmbeddedScripts.BatchEngine, payload, TimeSpan.FromMinutes(10), RequiresElevation: true),
            progress is null ? null : snapshot => reported = PublishProgress(snapshot, labels, ids.Length, reported, progress),
            cancellationToken);

        var results = ParseResults(run.ResultJson);
        var recorded = new List<TweakOperationResult>();
        foreach (var id in ids)
        {
            var item = results?.FirstOrDefault(result => result.TweakId == id);
            var result = new TweakOperationResult(
                Guid.NewGuid(), id, startedAt, item?.PreviousState ?? "Não consultado", labels[id],
                item?.Outcome ?? "Falha ou não verificado",
                item?.Message ?? (results is null ? FailureMessage(run) : "O executor não retornou resultado para este aplicativo."),
                RequiresRestart: false,
                RollbackMethod: item?.RollbackData is null ? null : "Re-registrar o manifesto local ou reinstalar pela Microsoft Store",
                RollbackOutcome: item?.RollbackOutcome, RollbackData: item?.RollbackData);
            await history.AppendAsync(result, cancellationToken);
            recorded.Add(result);
        }
        return recorded;
    }

    private static List<ScriptResult>? ParseResults(string? json)
    {
        if (json is null)
            return null;
        try { return JsonSerializer.Deserialize<ScriptEnvelope>(json, JsonOptions)?.Results; }
        catch (JsonException) { return null; }
    }

    private static string FailureMessage(ScriptRunResult run) =>
        run.ErrorOutput is { Length: > 0 } error ? $"{run.Message} Detalhe: {(error.Length > 400 ? error[..400] + "…" : error)}" : run.Message;

    private static int PublishProgress(string snapshot, IReadOnlyDictionary<string, string> labels, int total, int reported, IProgress<AppxBatchProgress> progress)
    {
        try
        {
            var results = JsonSerializer.Deserialize<ScriptEnvelope>(snapshot, JsonOptions)?.Results;
            for (var index = reported; results is not null && index < results.Count; index++)
            {
                progress.Report(new AppxBatchProgress(index + 1, total, labels.GetValueOrDefault(results[index].TweakId, results[index].TweakId),
                    results[index].Outcome ?? "Não verificado"));
                reported = index + 1;
            }
        }
        catch (JsonException) { }
        return reported;
    }

    private static bool TryReadRemoval(TweakOperationResult entry, out RemovedAppxPackage removed)
    {
        removed = null!;
        if (entry.RollbackData is null)
            return false;
        try
        {
            var data = JsonSerializer.Deserialize<RemovalData>(entry.RollbackData, JsonOptions);
            if (data?.Name is not { Length: > 0 } name || !string.Equals(OperationPrefix + name, entry.TweakId, StringComparison.OrdinalIgnoreCase))
                return false;
            var friendly = entry.RequestedState.StartsWith("Remover ", StringComparison.Ordinal) ? entry.RequestedState["Remover ".Length..] : name;
            removed = new RemovedAppxPackage(name, friendly, data.PackageFamilyName ?? string.Empty, entry.StartedAt, data.AllUsers,
                data.FilesKeptAfterRemoval, entry.RollbackData);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record RawInventory(string? CurrentUserSid, string? InteractiveUserSid, bool ProvisionedKnown, List<RawPackage>? Packages);
    private sealed record RawPackage(
        string Name, string? PackageFullName, string? PackageFamilyName, string? Publisher, string? Version, string? InstallLocation,
        string? SignatureKind, bool IsFramework, bool IsResourcePackage, bool NonRemovable, bool? IsProvisioned);
    private sealed record RemovalData(string? Name, string? PackageFamilyName, bool AllUsers, bool FilesKeptAfterRemoval);
    private sealed record ScriptEnvelope(List<ScriptResult>? Results);
    private sealed record ScriptResult(string TweakId, string? Outcome, string? Message, string? PreviousState, string? RollbackData, string? RollbackOutcome);
}
