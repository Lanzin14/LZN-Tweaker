using System.IO;
using System.Text.Json;
using System.Security.Principal;
using LznTweaker.Core.Execution;
using Microsoft.Win32;

namespace LznTweaker.Core.Tweaks;

public sealed record TweakBatchProgress(int Completed, int Total, string Name, string Outcome, string Message);

/// <summary>
/// Valida um lote contra o catálogo e o executa em um único processo PowerShell (via <see cref="ScriptRunnerService"/>).
/// Todo lote que altera o sistema começa por um ponto de restauração obrigatório: se ele não puder ser criado
/// (ou reaproveitado dentro do limite de 24 h do Windows), o script bloqueia todas as demais operações.
/// </summary>
public sealed class TweakBatchExecutor(TweakOperationHistory history, ScriptRunnerService scriptRunner)
{
    private const string RollbackMethodText = "Restaurar o estado anterior registrado";
    private static readonly TimeSpan BatchTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan UndoTimeout = TimeSpan.FromMinutes(3);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // Pares que gravam o mesmo valor ou fazem ações opostas; executá-los juntos tornaria o resultado imprevisível.
    private static readonly (string First, string Second)[] ConflictingPairs =
    [
        (TweakCatalog.SetIpv4PriorityId, TweakCatalog.DisableIpv6Id),
        (TweakCatalog.ActivateUltimatePerformanceId, TweakCatalog.RemoveUltimatePerformanceId)
    ];

    private static readonly RegistryValueKind[] SupportedOldKinds =
        [RegistryValueKind.DWord, RegistryValueKind.QWord, RegistryValueKind.String, RegistryValueKind.ExpandString];

    public async Task<IReadOnlyList<TweakOperationResult>> RunAsync(
        IReadOnlyList<TweakDefinition> definitions,
        IReadOnlyList<TempCleanupFile> selectedTempFiles,
        IProgress<TweakBatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var preflightResults = new List<TweakOperationResult>();
        var conflicting = GetConflictingIds(definitions);
        foreach (var definition in definitions.Where(definition => conflicting.ContainsKey(definition.Id)))
        {
            var conflict = new TweakOperationResult(Guid.NewGuid(), definition.Id, DateTimeOffset.Now,
                "Não consultado", definition.Name, "Conflito",
                $"Não é possível executar junto com \"{conflicting[definition.Id]}\"; selecione apenas um. Nada foi alterado.",
                definition.RequiresRestart, null, "Não se aplica");
            preflightResults.Add(await RecordAndReportAsync(conflict, cancellationToken));
        }

        var runnable = definitions.Where(definition => !conflicting.ContainsKey(definition.Id)).ToArray();
        foreach (var definition in runnable.Where(definition => !IsCompatible(definition)))
        {
            var unsupported = new TweakOperationResult(Guid.NewGuid(), definition.Id, DateTimeOffset.Now,
                "Compatibilidade não atendida", definition.Name, "Não suportado",
                GetIncompatibilityMessage(definition), definition.RequiresRestart, null, "Não se aplica");
            preflightResults.Add(await RecordAndReportAsync(unsupported, cancellationToken));
        }

        var compatibleDefinitions = runnable.Where(IsCompatible).ToArray();
        var definitionsToRun = WithMandatoryRestorePoint(compatibleDefinitions
            .Where(definition => definition.Id != TweakCatalog.DeleteTemporaryFilesId || selectedTempFiles.Count > 0)
            .ToArray());
        var skippedCleanup = compatibleDefinitions.Any(definition => definition.Id == TweakCatalog.DeleteTemporaryFilesId) && selectedTempFiles.Count == 0;
        if (definitionsToRun.Length == 0)
        {
            if (!skippedCleanup)
                return preflightResults;

            var skipped = new TweakOperationResult(Guid.NewGuid(), TweakCatalog.DeleteTemporaryFilesId,
                DateTimeOffset.Now, "Prévia analisada", "Limpar temporários", "Ignorado",
                "Nenhum arquivo foi selecionado na prévia; nada foi apagado.", false, null, "Não se aplica");
            preflightResults.Add(await RecordAndReportAsync(skipped, cancellationToken));
            return preflightResults;
        }

        var operationStarted = DateTimeOffset.Now;
        try
        {
            var requests = definitionsToRun.Select(definition => ToScriptOperation(definition, selectedTempFiles)).ToArray();
            var payload = JsonSerializer.Serialize(new ScriptPayload(requests, GetCurrentUserSid(), RequireRestorePoint: true), JsonOptions);

            progress?.Report(new TweakBatchProgress(0, definitionsToRun.Length, "Lote coordenado", "Em andamento",
                "O executor único iniciou; aguardando o resultado do ponto de restauração."));
            var reportedCount = 0;
            var run = await scriptRunner.RunAsync(
                new ScriptRunRequest("batch", EmbeddedScripts.BatchEngine, payload, BatchTimeout,
                    definitionsToRun.Any(definition => definition.RequiresAdministrator)),
                progress is null ? null : snapshot => reportedCount = PublishProgress(snapshot, definitionsToRun, reportedCount, progress),
                cancellationToken);

            var envelope = ParseEnvelope(run);
            if (envelope?.Results is null)
                return preflightResults.Concat(await RecordBatchFailureAsync(definitionsToRun, operationStarted, Describe(run), cancellationToken)).ToArray();

            var results = new List<TweakOperationResult>();
            foreach (var definition in definitionsToRun)
            {
                var item = envelope.Results.FirstOrDefault(result =>
                    string.Equals(result.TweakId, definition.Id, StringComparison.Ordinal));
                var mapped = item is null
                    ? Failure(definition, operationStarted, "O executor não retornou resultado para esta operação.")
                    : new TweakOperationResult(
                        Guid.NewGuid(), definition.Id, operationStarted,
                        item.PreviousState ?? "Não disponível", definition.Name,
                        item.Outcome ?? "Não verificado", item.Message ?? "Sem detalhes retornados.",
                        definition.RequiresRestart,
                        string.IsNullOrWhiteSpace(item.RollbackData) ? null : RollbackMethodText,
                        item.RollbackOutcome,
                        item.RollbackData);
                results.Add(await RecordAndReportAsync(mapped, cancellationToken));
            }

            if (skippedCleanup)
            {
                var skipped = new TweakOperationResult(Guid.NewGuid(), TweakCatalog.DeleteTemporaryFilesId,
                    operationStarted, "Prévia analisada", "Limpar temporários", "Ignorado",
                    "Nenhum arquivo foi selecionado na prévia; o restante do lote foi executado normalmente.",
                    false, null, "Não se aplica");
                results.Add(await RecordAndReportAsync(skipped, cancellationToken));
            }
            return preflightResults.Concat(results).ToArray();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return preflightResults.Concat(await RecordBatchFailureAsync(definitionsToRun, operationStarted,
                $"Falha ao preparar ou verificar o lote: {ex.Message}", cancellationToken)).ToArray();
        }
    }

    public async Task<IReadOnlyList<TweakOperationResult>> UndoAsync(
        IReadOnlyList<TweakOperationResult> entries,
        CancellationToken cancellationToken = default)
    {
        var candidates = new List<TweakOperationResult>();
        var refused = new List<TweakOperationResult>();
        foreach (var entry in entries.Where(entry => !string.IsNullOrWhiteSpace(entry.RollbackData)))
        {
            if (IsCatalogRollbackData(entry))
            {
                candidates.Add(entry);
                continue;
            }

            var refusal = new TweakOperationResult(Guid.NewGuid(), entry.TweakId, DateTimeOffset.Now,
                "Histórico não validado", "Desfazer", "Reversão recusada",
                "Os dados de reversão não correspondem à operação registrada no catálogo; nenhum valor foi alterado.",
                false, null, "Recusado por validação local");
            refused.Add(await RecordAndReportAsync(refusal, cancellationToken));
        }
        if (candidates.Count == 0)
            return refused;

        var startedAt = DateTimeOffset.Now;
        try
        {
            // O Undo restaura o estado anterior registrado; ele não exige um novo ponto de restauração,
            // para que uma falha da Proteção do Sistema nunca impeça desfazer uma alteração.
            var payload = JsonSerializer.Serialize(new ScriptPayload(candidates.Select(entry => new ScriptOperation(
                entry.TweakId,
                TweakCatalog.All.FirstOrDefault(definition => definition.Id == entry.TweakId)?.Name ?? entry.TweakId,
                "Undo",
                RollbackData: entry.RollbackData)).ToArray(), GetCurrentUserSid(), RequireRestorePoint: false), JsonOptions);
            var requiresElevation = candidates.Any(entry =>
                TweakCatalog.All.FirstOrDefault(definition => definition.Id == entry.TweakId)?.RequiresAdministrator == true);

            var run = await scriptRunner.RunAsync(
                new ScriptRunRequest("undo", EmbeddedScripts.BatchEngine, payload, UndoTimeout, requiresElevation),
                cancellationToken: cancellationToken);
            var envelope = ParseEnvelope(run);
            if (envelope?.Results is null)
                return refused.Concat(await RecordUndoFailureAsync(candidates, startedAt, Describe(run), cancellationToken)).ToArray();

            var results = new List<TweakOperationResult>();
            foreach (var candidate in candidates)
            {
                var item = envelope.Results.FirstOrDefault(result => result.TweakId == candidate.TweakId);
                var result = new TweakOperationResult(
                    Guid.NewGuid(), candidate.TweakId, startedAt,
                    item?.PreviousState ?? "Estado atual não confirmado", "Desfazer",
                    item?.Outcome ?? "Falha ou não verificado", item?.Message ?? "Sem resultado retornado.",
                    false, RollbackMethodText, item?.RollbackOutcome);
                results.Add(await RecordAndReportAsync(result, cancellationToken));
            }
            return refused.Concat(results).ToArray();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return refused.Concat(await RecordUndoFailureAsync(candidates, startedAt, $"Falha ao preparar a reversão: {ex.Message}", cancellationToken)).ToArray();
        }
    }

    /// <summary>Coloca o ponto de restauração na frente de qualquer lote que altere o sistema.</summary>
    internal static TweakDefinition[] WithMandatoryRestorePoint(TweakDefinition[] definitions)
    {
        if (definitions.Length == 0)
            return definitions;
        var others = definitions.Where(definition => definition.Id != TweakCatalog.CreateRestorePointId);
        return [TweakCatalog.GetById(TweakCatalog.CreateRestorePointId), .. others];
    }

    internal static Dictionary<string, string> GetConflictingIds(IReadOnlyList<TweakDefinition> definitions)
    {
        var selected = definitions.Select(definition => definition.Id).ToHashSet(StringComparer.Ordinal);
        var conflicting = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (first, second) in ConflictingPairs.Where(pair => selected.Contains(pair.First) && selected.Contains(pair.Second)))
        {
            conflicting[first] = TweakCatalog.GetById(second).Name;
            conflicting[second] = TweakCatalog.GetById(first).Name;
        }
        return conflicting;
    }

    internal static ScriptOperation ToScriptOperation(TweakDefinition definition, IReadOnlyList<TempCleanupFile> selectedTempFiles)
    {
        if (definition.Id == TweakCatalog.CreateRestorePointId)
            return new ScriptOperation(definition.Id, definition.Name, "RestorePoint");
        if (definition.Id == TweakCatalog.DeleteTemporaryFilesId)
            return new ScriptOperation(definition.Id, definition.Name, "DeleteTempFiles",
                TempRoot: Path.GetFullPath(Environment.GetEnvironmentVariable("TEMP") is { Length: > 0 } temp ? temp : Path.GetTempPath()),
                Files: selectedTempFiles.Select(file => new ScriptFile(file.FullPath, file.Length, file.LastWriteTimeUtcTicks)).ToArray());

        var hasTrackedChanges = definition.RegistryChanges is { Count: > 0 } ||
                                definition.HklmRegistryChanges is { Count: > 0 } ||
                                definition.ServiceChanges is { Count: > 0 } ||
                                definition.ScheduledTasksToDisable is { Count: > 0 };
        var hasScript = !string.IsNullOrWhiteSpace(definition.InvokeScript);

        if (hasTrackedChanges && hasScript)
            throw new InvalidOperationException($"A operação '{definition.Id}' mistura script e alterações rastreadas, o que impede uma reversão confiável.");

        if (hasTrackedChanges)
        {
            return new ScriptOperation(definition.Id, definition.Name, "Tracked",
                RegistryChanges: definition.RegistryChanges?.ToArray(),
                HklmRegistryChanges: definition.HklmRegistryChanges?.ToArray(),
                ServiceChanges: definition.ServiceChanges?.ToArray(),
                ScheduledTasks: definition.ScheduledTasksToDisable?.ToArray());
        }

        if (hasScript)
            return new ScriptOperation(definition.Id, definition.Name, "CustomScript",
                InvokeScript: definition.InvokeScript,
                VerifyScript: definition.VerifyScript);

        throw new InvalidOperationException($"A operação '{definition.Id}' não possui implementação executável.");
    }

    private static ScriptEnvelope? ParseEnvelope(ScriptRunResult run)
    {
        if (run.ResultJson is null)
            return null;
        try { return JsonSerializer.Deserialize<ScriptEnvelope>(run.ResultJson, JsonOptions); }
        catch (JsonException) { return null; }
    }

    private static string Describe(ScriptRunResult run) => run.ResultJson is not null
        ? "O arquivo de resultado do lote não pôde ser interpretado."
        : run.ErrorOutput is { Length: > 0 } error
            ? $"{run.Message} Detalhe: {(error.Length > 400 ? error[..400] + "…" : error)}"
            : run.Message;

    private static int PublishProgress(string snapshot, IReadOnlyList<TweakDefinition> definitions, int reportedCount, IProgress<TweakBatchProgress> progress)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<ScriptEnvelope>(snapshot, JsonOptions);
            if (envelope?.Results is null)
                return reportedCount;
            for (var index = reportedCount; index < envelope.Results.Count && index < definitions.Count; index++)
            {
                var item = envelope.Results[index];
                var definition = definitions.FirstOrDefault(candidate => candidate.Id == item.TweakId);
                progress.Report(new TweakBatchProgress(index + 1, definitions.Count, definition?.Name ?? item.TweakId,
                    item.Outcome ?? "Não verificado", item.Message ?? "Sem detalhes retornados."));
                reportedCount = index + 1;
            }
        }
        catch (JsonException)
        {
            // Instantâneo parcial; o próximo será lido.
        }
        return reportedCount;
    }

    private async Task<IReadOnlyList<TweakOperationResult>> RecordBatchFailureAsync(
        IReadOnlyList<TweakDefinition> definitions, DateTimeOffset startedAt, string message, CancellationToken cancellationToken)
    {
        var results = new List<TweakOperationResult>();
        foreach (var definition in definitions)
            results.Add(await RecordAndReportAsync(Failure(definition, startedAt, message), cancellationToken));
        return results;
    }

    private async Task<IReadOnlyList<TweakOperationResult>> RecordUndoFailureAsync(
        IReadOnlyList<TweakOperationResult> candidates, DateTimeOffset startedAt, string message, CancellationToken cancellationToken)
    {
        var results = new List<TweakOperationResult>();
        foreach (var candidate in candidates)
        {
            var result = new TweakOperationResult(
                Guid.NewGuid(), candidate.TweakId, startedAt, "Estado atual não confirmado", "Desfazer",
                "Falha ou não verificado", message, false, RollbackMethodText, "Não concluído");
            results.Add(await RecordAndReportAsync(result, cancellationToken));
        }
        return results;
    }

    private async Task<TweakOperationResult> RecordAndReportAsync(TweakOperationResult result, CancellationToken cancellationToken)
    {
        try
        {
            await history.AppendAsync(result, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            var logWarning = $"O resultado foi recebido, mas o histórico local não pôde ser salvo: {ex.Message}";
            return result with
            {
                Outcome = result.Outcome == "Sucesso verificado" ? "Sucesso verificado (histórico indisponível)" : result.Outcome,
                Message = $"{result.Message} {logWarning}",
                RollbackData = null,
                RollbackMethod = result.RollbackData is null ? result.RollbackMethod : null,
                RollbackOutcome = result.RollbackData is null ? result.RollbackOutcome : "Reversão não guardada no histórico."
            };
        }
    }

    private static bool IsCompatible(TweakDefinition definition) => definition.Id switch
    {
        TweakCatalog.CreateRestorePointId => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240),
        TweakCatalog.EndTaskOnTaskbarId => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000),
        _ => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240)
    };

    private static string GetIncompatibilityMessage(TweakDefinition definition) => definition.Id switch
    {
        TweakCatalog.EndTaskOnTaskbarId => "Este ajuste requer Windows 11 (build 22000 ou superior); nada foi alterado.",
        _ => "Esta operação requer Windows 10 ou Windows 11; nada foi alterado."
    };

    private static TweakOperationResult Failure(TweakDefinition definition, DateTimeOffset startedAt, string message) =>
        new(Guid.NewGuid(), definition.Id, startedAt, "Estado anterior não consultado", definition.Name,
            "Falha ou não verificado", message, definition.RequiresRestart, null, "Não disponível");

    /// <summary>
    /// Aceita somente dados de reversão cujas chaves, serviços e tarefas pertencem à definição do catálogo
    /// e cujos valores aplicados são exatamente os do catálogo. Entradas antigas (array) só continham HKCU.
    /// </summary>
    internal static bool IsCatalogRollbackData(TweakOperationResult entry)
    {
        var definition = TweakCatalog.All.FirstOrDefault(item => item.Id == entry.TweakId);
        if (definition is null || string.IsNullOrWhiteSpace(entry.RollbackData))
            return false;

        try
        {
            using var document = JsonDocument.Parse(entry.RollbackData);
            var root = document.RootElement;
            var isLegacy = root.ValueKind == JsonValueKind.Array;
            if (!isLegacy && root.ValueKind != JsonValueKind.Object)
                return false;

            var expected = (definition.RegistryChanges ?? []).Select(change => (Hive: "HKCU", Change: change))
                .Concat((definition.HklmRegistryChanges ?? []).Select(change => (Hive: "HKLM", Change: change)))
                .ToArray();
            var registry = isLegacy ? root : GetArray(root, "Registry");
            if (registry is not { } registryEntries || registryEntries.GetArrayLength() != expected.Length)
                return false;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in registryEntries.EnumerateArray())
            {
                if (!item.TryGetProperty("SubKey", out var subKeyElement) || !item.TryGetProperty("Name", out var nameElement) ||
                    !item.TryGetProperty("AppliedKind", out var kindElement) || !item.TryGetProperty("AppliedValue", out var valueElement) ||
                    !item.TryGetProperty("OldExists", out var oldExistsElement) || !item.TryGetProperty("OldKind", out var oldKindElement))
                    return false;

                var hive = item.TryGetProperty("Hive", out var hiveElement) ? hiveElement.GetString() : "HKCU";
                if (isLegacy && hive != "HKCU")
                    return false;
                var subKey = subKeyElement.GetString();
                var name = nameElement.GetString();
                var match = expected.FirstOrDefault(candidate =>
                    string.Equals(candidate.Hive, hive, StringComparison.Ordinal) &&
                    string.Equals(candidate.Change.SubKey, subKey, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(candidate.Change.Name, name, StringComparison.OrdinalIgnoreCase));
                if (match.Change is null || !seen.Add($"{hive}|{subKey}|{name}"))
                    return false;

                var change = match.Change;
                if (!kindElement.TryGetInt32(out var appliedKind) || appliedKind != (int)change.Kind ||
                    !MatchesValue(valueElement, change.Kind, change.Value) ||
                    oldExistsElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                    !oldKindElement.TryGetInt32(out var oldKindValue))
                    return false;

                var oldKind = (RegistryValueKind)oldKindValue;
                if (oldExistsElement.GetBoolean() &&
                    (!SupportedOldKinds.Contains(oldKind) || !item.TryGetProperty("OldValue", out var oldValueElement) ||
                     !IsValueOfKind(oldValueElement, oldKind)))
                    return false;
            }
            if (seen.Count != expected.Length)
                return false;
            if (isLegacy)
                return true;

            var serviceTargets = (definition.ServiceChanges ?? [])
                .ToDictionary(change => change.Name, change => StartTypeCode(change.TargetStartupType), StringComparer.OrdinalIgnoreCase);
            var seenServices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (GetArray(root, "Services") is { } services)
            {
                foreach (var item in services.EnumerateArray())
                {
                    if (!item.TryGetProperty("Name", out var nameElement) || nameElement.GetString() is not { } name ||
                        !serviceTargets.TryGetValue(name, out var target) || !seenServices.Add(name) ||
                        !item.TryGetProperty("AppliedStart", out var appliedElement) || !appliedElement.TryGetInt32(out var applied) || applied != target ||
                        !item.TryGetProperty("OldStart", out var oldElement) || !oldElement.TryGetInt32(out var oldStart) || oldStart is < 0 or > 4)
                        return false;
                }
            }

            var taskPaths = (definition.ScheduledTasksToDisable ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var seenTasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (GetArray(root, "Tasks") is { } tasks)
            {
                foreach (var item in tasks.EnumerateArray())
                {
                    if (!item.TryGetProperty("Path", out var pathElement) || pathElement.GetString() is not { } path ||
                        !taskPaths.Contains(path) || !seenTasks.Add(path))
                        return false;
                }
            }
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or OverflowException or ArgumentException)
        {
            return false;
        }
    }

    private static JsonElement? GetArray(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.Array ? element : null;

    private static bool MatchesValue(JsonElement element, RegistryValueKind kind, object expected) => kind switch
    {
        RegistryValueKind.DWord or RegistryValueKind.QWord =>
            element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var number) && number == Convert.ToInt64(expected),
        _ => element.ValueKind == JsonValueKind.String && string.Equals(element.GetString(), Convert.ToString(expected), StringComparison.Ordinal)
    };

    private static bool IsValueOfKind(JsonElement element, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.DWord or RegistryValueKind.QWord => element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out _),
        _ => element.ValueKind == JsonValueKind.String
    };

    private static int StartTypeCode(string startupType) => startupType switch
    {
        "Automatic" => 2,
        "Manual" => 3,
        "Disabled" => 4,
        _ => throw new ArgumentException($"Tipo de inicialização não suportado: {startupType}")
    };

    private static string GetCurrentUserSid() => WindowsIdentity.GetCurrent().User?.Value
        ?? throw new InvalidOperationException("Não foi possível determinar o SID do usuário atual.");

    internal sealed record ScriptPayload(ScriptOperation[] Operations, string UserSid, bool RequireRestorePoint);
    internal sealed record ScriptOperation(
        string TweakId,
        string Name,
        string Kind,
        RegistryValueChange[]? RegistryChanges = null,
        RegistryValueChange[]? HklmRegistryChanges = null,
        ServiceChange[]? ServiceChanges = null,
        string[]? ScheduledTasks = null,
        string? InvokeScript = null,
        string? VerifyScript = null,
        string? TempRoot = null,
        ScriptFile[]? Files = null,
        string? RollbackData = null);
    internal sealed record ScriptFile(string FullPath, long Length, long LastWriteTimeUtcTicks);
    private sealed record ScriptEnvelope(List<ScriptResult>? Results);
    private sealed record ScriptResult(
        string TweakId,
        string? Outcome,
        string? Message,
        string? PreviousState,
        string? RollbackData,
        string? RollbackOutcome);
}
