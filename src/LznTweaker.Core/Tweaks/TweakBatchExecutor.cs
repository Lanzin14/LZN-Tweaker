using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Security.Principal;
using Microsoft.Win32;

namespace LznTweaker.Core.Tweaks;

public sealed record TweakBatchProgress(int Completed, int Total, string Name, string Outcome, string Message);

/// <summary>Executes one catalog-validated batch in a temporary PowerShell script.</summary>
public sealed class TweakBatchExecutor(TweakOperationHistory history)
{
    private const string PowerShellRelativePath = @"WindowsPowerShell\v1.0\powershell.exe";
    private const string RollbackMethodText = "Restaurar o estado anterior registrado";
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
        var definitionsToRun = compatibleDefinitions
            .Where(definition => definition.Id != TweakCatalog.DeleteTemporaryFilesId || selectedTempFiles.Count > 0)
            .OrderBy(definition => definition.Id == TweakCatalog.CreateRestorePointId ? 0 : 1)
            .ToArray();
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

        var operationId = Guid.NewGuid();
        var operationDirectory = GetOperationDirectory();
        var scriptPath = Path.Combine(operationDirectory, $"batch-{operationId:N}.ps1");
        var payloadPath = Path.Combine(operationDirectory, $"batch-{operationId:N}.json");
        var resultPath = Path.Combine(operationDirectory, $"batch-{operationId:N}-result.json");
        var progressPath = Path.Combine(operationDirectory, $"batch-{operationId:N}-progress.json");
        var operationStarted = DateTimeOffset.Now;

        try
        {
            Directory.CreateDirectory(operationDirectory);
            var requests = definitionsToRun.Select(definition => ToScriptOperation(definition, selectedTempFiles)).ToArray();
            await File.WriteAllTextAsync(payloadPath,
                JsonSerializer.Serialize(new ScriptPayload(requests, GetCurrentUserSid()), JsonOptions), cancellationToken);
            await File.WriteAllTextAsync(scriptPath, BatchScript, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);

            var requiresElevation = definitionsToRun.Any(definition => definition.RequiresAdministrator);
            using var process = new Process { StartInfo = CreateStartInfo(scriptPath, payloadPath, resultPath, progressPath, requiresElevation) };
            if (!process.Start())
                return preflightResults.Concat(await RecordBatchFailureAsync(definitionsToRun, operationStarted, "O Windows não iniciou o executor PowerShell.", cancellationToken)).ToArray();

            progress?.Report(new TweakBatchProgress(0, definitionsToRun.Length, "Lote coordenado", "Em andamento",
                "O executor único iniciou; aguardando o resultado da primeira operação."));
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));
            try
            {
                var exitTask = process.WaitForExitAsync(timeout.Token);
                var reportedCount = 0;
                while (!exitTask.IsCompleted)
                {
                    if (progress is not null)
                        reportedCount = await PublishProgressAsync(progressPath, definitionsToRun, reportedCount, progress, cancellationToken);
                    await Task.WhenAny(exitTask, Task.Delay(250, timeout.Token));
                }
                await exitTask;
                if (progress is not null)
                    await PublishProgressAsync(progressPath, definitionsToRun, reportedCount, progress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                return preflightResults.Concat(await RecordBatchFailureAsync(definitionsToRun, operationStarted,
                    cancellationToken.IsCancellationRequested
                        ? "A execução foi cancelada; operações em andamento podem ter resultado incerto."
                        : "O lote excedeu o limite de cinco minutos; o resultado não foi confirmado.", cancellationToken)).ToArray();
            }

            if (!File.Exists(resultPath))
            {
                var message = process.ExitCode == 0
                    ? "O processo terminou sem um arquivo de resultado verificável."
                    : $"O PowerShell terminou com código {process.ExitCode} sem resultado verificável.";
                return preflightResults.Concat(await RecordBatchFailureAsync(definitionsToRun, operationStarted, message, cancellationToken)).ToArray();
            }

            var json = await File.ReadAllTextAsync(resultPath, cancellationToken);
            var envelope = JsonSerializer.Deserialize<ScriptEnvelope>(json, JsonOptions);
            if (envelope?.Results is null)
                return preflightResults.Concat(await RecordBatchFailureAsync(definitionsToRun, operationStarted,
                    "O arquivo de resultado do lote não pôde ser interpretado.", cancellationToken)).ToArray();

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
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return preflightResults.Concat(await RecordBatchFailureAsync(definitionsToRun, operationStarted,
                "A solicitação de administrador foi cancelada; o lote não foi executado.", cancellationToken)).ToArray();
        }
        catch (Exception ex)
        {
            return preflightResults.Concat(await RecordBatchFailureAsync(definitionsToRun, operationStarted,
                $"Falha ao preparar ou verificar o lote: {ex.Message}", cancellationToken)).ToArray();
        }
        finally
        {
            TryDelete(scriptPath);
            TryDelete(payloadPath);
            TryDelete(resultPath);
            TryDelete(progressPath);
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

        var operationId = Guid.NewGuid();
        var operationDirectory = GetOperationDirectory();
        var scriptPath = Path.Combine(operationDirectory, $"undo-{operationId:N}.ps1");
        var payloadPath = Path.Combine(operationDirectory, $"undo-{operationId:N}.json");
        var resultPath = Path.Combine(operationDirectory, $"undo-{operationId:N}-result.json");
        var progressPath = Path.Combine(operationDirectory, $"undo-{operationId:N}-progress.json");
        var startedAt = DateTimeOffset.Now;

        try
        {
            Directory.CreateDirectory(operationDirectory);
            await File.WriteAllTextAsync(payloadPath,
                JsonSerializer.Serialize(new ScriptPayload(candidates.Select(entry => new ScriptOperation(
                    entry.TweakId,
                    TweakCatalog.All.FirstOrDefault(definition => definition.Id == entry.TweakId)?.Name ?? entry.TweakId,
                    "Undo",
                    RollbackData: entry.RollbackData)).ToArray(), GetCurrentUserSid()), JsonOptions), cancellationToken);
            await File.WriteAllTextAsync(scriptPath, BatchScript, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);

            var requiresElevation = candidates.Any(entry =>
                TweakCatalog.All.FirstOrDefault(definition => definition.Id == entry.TweakId)?.RequiresAdministrator == true);
            using var process = new Process { StartInfo = CreateStartInfo(scriptPath, payloadPath, resultPath, progressPath, requiresElevation) };
            if (!process.Start())
                return await RecordUndoFailureAsync(candidates, startedAt, "O Windows não iniciou a reversão.", cancellationToken);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                return await RecordUndoFailureAsync(candidates, startedAt,
                    cancellationToken.IsCancellationRequested
                        ? "A reversão foi cancelada; o estado pode estar parcialmente revertido."
                        : "A reversão excedeu três minutos; o resultado é incerto.", cancellationToken);
            }

            if (!File.Exists(resultPath))
                return await RecordUndoFailureAsync(candidates, startedAt, "A reversão não retornou um resultado verificável.", cancellationToken);

            var envelope = JsonSerializer.Deserialize<ScriptEnvelope>(
                await File.ReadAllTextAsync(resultPath, cancellationToken), JsonOptions);
            var results = new List<TweakOperationResult>();
            foreach (var candidate in candidates)
            {
                var item = envelope?.Results?.FirstOrDefault(result => result.TweakId == candidate.TweakId);
                var result = new TweakOperationResult(
                    Guid.NewGuid(), candidate.TweakId, startedAt,
                    item?.PreviousState ?? "Estado atual não confirmado", "Desfazer",
                    item?.Outcome ?? "Falha ou não verificado", item?.Message ?? "Sem resultado retornado.",
                    false, RollbackMethodText, item?.RollbackOutcome);
                results.Add(await RecordAndReportAsync(result, cancellationToken));
            }
            return refused.Concat(results).ToArray();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return await RecordUndoFailureAsync(candidates, startedAt, "A solicitação de administrador foi cancelada.", cancellationToken);
        }
        catch (Exception ex)
        {
            return await RecordUndoFailureAsync(candidates, startedAt, $"Falha ao preparar a reversão: {ex.Message}", cancellationToken);
        }
        finally
        {
            TryDelete(scriptPath);
            TryDelete(payloadPath);
            TryDelete(resultPath);
            TryDelete(progressPath);
        }
    }

    private static Dictionary<string, string> GetConflictingIds(IReadOnlyList<TweakDefinition> definitions)
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

    private static ScriptOperation ToScriptOperation(TweakDefinition definition, IReadOnlyList<TempCleanupFile> selectedTempFiles)
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

    private static ProcessStartInfo CreateStartInfo(string scriptPath, string payloadPath, string resultPath, string progressPath, bool requiresElevation)
    {
        var powerShellPath = Path.Combine(Environment.SystemDirectory, PowerShellRelativePath);
        var arguments = $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\" -PayloadPath \"{payloadPath}\" -ResultPath \"{resultPath}\" -ProgressPath \"{progressPath}\"";
        return new ProcessStartInfo
        {
            FileName = powerShellPath,
            Arguments = arguments,
            UseShellExecute = requiresElevation,
            Verb = requiresElevation ? "runas" : string.Empty,
            CreateNoWindow = !requiresElevation,
            WindowStyle = ProcessWindowStyle.Hidden
        };
    }

    private static async Task<int> PublishProgressAsync(
        string progressPath,
        IReadOnlyList<TweakDefinition> definitions,
        int reportedCount,
        IProgress<TweakBatchProgress> progress,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(progressPath))
            return reportedCount;
        try
        {
            var envelope = JsonSerializer.Deserialize<ScriptEnvelope>(
                await File.ReadAllTextAsync(progressPath, cancellationToken), JsonOptions);
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
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // The PowerShell writer may be replacing the progress snapshot; the next poll retries it.
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

    private static string GetOperationDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LznTweaker", "Operations");

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

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private sealed record ScriptPayload(ScriptOperation[] Operations, string UserSid);
    private sealed record ScriptOperation(
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
    private sealed record ScriptFile(string FullPath, long Length, long LastWriteTimeUtcTicks);
    private sealed record ScriptEnvelope(List<ScriptResult>? Results);
    private sealed record ScriptResult(
        string TweakId,
        string? Outcome,
        string? Message,
        string? PreviousState,
        string? RollbackData,
        string? RollbackOutcome);

    internal const string BatchScript = """
        param([Parameter(Mandatory=$true)][string]$PayloadPath, [Parameter(Mandatory=$true)][string]$ResultPath, [Parameter(Mandatory=$true)][string]$ProgressPath)
        $ErrorActionPreference = 'Stop'
        $payload = Get-Content -LiteralPath $PayloadPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $results = New-Object System.Collections.Generic.List[object]
        # DWord, QWord, String e ExpandString podem ser restaurados com fidelidade; outros tipos são recusados.
        $SupportedOldKinds = @(4, 11, 1, 2)
        $StartTypeCodes = @{ 'Automatic' = 2; 'Manual' = 3; 'Disabled' = 4 }

        function Test-SameValue($Left, $Right) {
            return [string]::Equals([string]$Left, [string]$Right, [System.StringComparison]::Ordinal)
        }

        # @($lista) falha no Windows PowerShell 5.1 com List[object] ("tipos de argumento não correspondem").
        function ConvertTo-Array($Value) {
            $items = New-Object System.Collections.Generic.List[object]
            if ($null -ne $Value) { foreach ($item in $Value) { $items.Add($item) } }
            return ,$items.ToArray()
        }

        function ConvertTo-RegistryValue($Value, [int]$Kind) {
            if ($Kind -eq 4) { return [int]$Value }
            if ($Kind -eq 11) { return [int64]$Value }
            return [string]$Value
        }

        function Open-HiveKey([string]$Hive, [string]$SubKey, [bool]$Writable) {
            $path = $SubKey.TrimStart('\')
            if ($Hive -eq 'HKLM') { $root = [Microsoft.Win32.Registry]::LocalMachine }
            elseif ($Hive -eq 'HKCU') { $root = [Microsoft.Win32.Registry]::Users; $path = ([string]$payload.UserSid) + '\' + $path }
            else { throw "Hive não permitida: $Hive" }
            $key = $root.OpenSubKey($path, $Writable)
            if ($null -eq $key -and $Writable) { $key = $root.CreateSubKey($path) }
            return $key
        }

        function Get-EntryHive($Entry) {
            if ($Entry.PSObject.Properties['Hive'] -and $Entry.Hive) { return [string]$Entry.Hive }
            return 'HKCU'
        }

        function New-Tracker {
            return [pscustomobject]@{
                Registry = New-Object System.Collections.Generic.List[object]
                Services = New-Object System.Collections.Generic.List[object]
                Tasks = New-Object System.Collections.Generic.List[object]
            }
        }

        function Set-TrackedRegistryValue([string]$Hive, $Change, $Tracker) {
            $key = Open-HiveKey $Hive ([string]$Change.SubKey) $true
            if ($null -eq $key) { throw "Não foi possível abrir $Hive\$($Change.SubKey)." }
            try {
                $name = [string]$Change.Name
                $kind = [int]$Change.Kind
                $exists = @($key.GetValueNames()) -contains $name
                $oldValue = $null
                $oldKind = $kind
                if ($exists) {
                    $oldKind = [int]$key.GetValueKind($name)
                    if ($SupportedOldKinds -notcontains $oldKind) { throw "O valor $name tem um tipo anterior ($oldKind) sem reversão segura; ele não foi alterado." }
                    $oldValue = $key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
                }
                $newValue = ConvertTo-RegistryValue $Change.Value $kind
                $key.SetValue($name, $newValue, [Microsoft.Win32.RegistryValueKind]$kind)
                $Tracker.Registry.Add([pscustomobject][ordered]@{
                    Hive = $Hive; SubKey = [string]$Change.SubKey; Name = $name; OldExists = [bool]$exists
                    OldValue = $oldValue; OldKind = $oldKind; AppliedValue = $newValue; AppliedKind = $kind
                })
                $readBack = $key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
                if ([int]$key.GetValueKind($name) -ne $kind -or -not (Test-SameValue $readBack $newValue)) {
                    throw "A verificação de $Hive\$($Change.SubKey)\$name falhou após a gravação."
                }
            } finally { $key.Dispose() }
        }

        function Assert-RegistryEntryUnchanged($Entry) {
            $hive = Get-EntryHive $Entry
            $key = Open-HiveKey $hive ([string]$Entry.SubKey) $false
            if ($null -eq $key) { throw "A chave $hive\$($Entry.SubKey) não existe mais; nada foi revertido." }
            try {
                $name = [string]$Entry.Name
                $exists = @($key.GetValueNames()) -contains $name
                if (-not $exists -or [int]$key.GetValueKind($name) -ne [int]$Entry.AppliedKind -or
                    -not (Test-SameValue $key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) $Entry.AppliedValue)) {
                    throw "O valor $name mudou desde a aplicação; ele foi preservado."
                }
            } finally { $key.Dispose() }
        }

        function Restore-RegistryEntry($Entry) {
            Assert-RegistryEntryUnchanged $Entry
            $hive = Get-EntryHive $Entry
            $key = Open-HiveKey $hive ([string]$Entry.SubKey) $true
            try {
                $name = [string]$Entry.Name
                if ([bool]$Entry.OldExists) {
                    $oldValue = ConvertTo-RegistryValue $Entry.OldValue ([int]$Entry.OldKind)
                    $key.SetValue($name, $oldValue, [Microsoft.Win32.RegistryValueKind][int]$Entry.OldKind)
                    $restored = $key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
                    if (-not (Test-SameValue $restored $oldValue)) { throw "A verificação de reversão de $name falhou." }
                } else {
                    $key.DeleteValue($name, $false)
                    if (@($key.GetValueNames()) -contains $name) { throw "O valor $name permaneceu após a reversão." }
                }
            } finally { $key.Dispose() }
        }

        function Get-ServiceStartInfo([string]$Name) {
            $key = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Services\$Name", $false)
            if ($null -eq $key) { return $null }
            try { return [pscustomobject]@{ Start = [int]$key.GetValue('Start', -1); Delayed = [int]$key.GetValue('DelayedAutostart', 0) } }
            finally { $key.Dispose() }
        }

        function Set-TrackedService($Change, $Tracker, $Notes) {
            $name = [string]$Change.Name
            $target = [string]$Change.TargetStartupType
            if (-not $StartTypeCodes.ContainsKey($target)) { throw "Tipo de inicialização não permitido: $target" }
            $before = Get-ServiceStartInfo $name
            if ($null -eq $before -or $null -eq (Get-Service -Name $name -ErrorAction SilentlyContinue)) { $Notes.Add("serviço $name ausente"); return }
            if ($before.Start -eq $StartTypeCodes[$target]) { $Notes.Add("serviço $name já estava $target"); return }
            Set-Service -Name $name -StartupType $target -ErrorAction Stop
            $Tracker.Services.Add([pscustomobject][ordered]@{ Name = $name; OldStart = $before.Start; OldDelayed = $before.Delayed; AppliedStart = $StartTypeCodes[$target] })
            $after = Get-ServiceStartInfo $name
            if ($null -eq $after -or $after.Start -ne $StartTypeCodes[$target]) { throw "O serviço $name não ficou como $target após a alteração." }
        }

        function Assert-ServiceUnchanged($Entry) {
            $current = Get-ServiceStartInfo ([string]$Entry.Name)
            if ($null -eq $current) { throw "O serviço $($Entry.Name) não existe mais." }
            if ($current.Start -ne [int]$Entry.AppliedStart) { throw "O serviço $($Entry.Name) mudou desde a aplicação; ele foi preservado." }
        }

        function Restore-TrackedService($Entry) {
            Assert-ServiceUnchanged $Entry
            $name = [string]$Entry.Name
            $mode = switch ([int]$Entry.OldStart) {
                0 { 'boot' } 1 { 'system' } 3 { 'demand' } 4 { 'disabled' }
                2 { if ([int]$Entry.OldDelayed -eq 1) { 'delayed-auto' } else { 'auto' } }
                default { throw "Tipo de inicialização anterior desconhecido para $name." }
            }
            $output = & sc.exe config $name start= $mode
            if ($LASTEXITCODE -ne 0) { throw "sc.exe não restaurou $($name): $output" }
            $after = Get-ServiceStartInfo $name
            if ($null -eq $after -or $after.Start -ne [int]$Entry.OldStart) { throw "A verificação da reversão do serviço $name falhou." }
        }

        function Split-TaskPath([string]$FullPath) {
            $index = $FullPath.LastIndexOf('\')
            return @($FullPath.Substring(0, $index + 1), $FullPath.Substring($index + 1))
        }

        function Disable-TrackedTask([string]$FullPath, $Tracker, $Notes) {
            $folder, $leaf = Split-TaskPath $FullPath
            $task = Get-ScheduledTask -TaskPath $folder -TaskName $leaf -ErrorAction SilentlyContinue
            if ($null -eq $task) { $Notes.Add("tarefa $leaf ausente"); return }
            if ([string]$task.State -eq 'Disabled') { $Notes.Add("tarefa $leaf já desativada"); return }
            Disable-ScheduledTask -TaskPath $folder -TaskName $leaf -ErrorAction Stop | Out-Null
            $Tracker.Tasks.Add([pscustomobject]@{ Path = $FullPath })
            $after = Get-ScheduledTask -TaskPath $folder -TaskName $leaf -ErrorAction Stop
            if ([string]$after.State -ne 'Disabled') { throw "A tarefa $leaf não ficou desativada." }
        }

        function Assert-TaskUnchanged($Entry) {
            $folder, $leaf = Split-TaskPath ([string]$Entry.Path)
            $task = Get-ScheduledTask -TaskPath $folder -TaskName $leaf -ErrorAction SilentlyContinue
            if ($null -eq $task) { throw "A tarefa $leaf não existe mais." }
            if ([string]$task.State -ne 'Disabled') { throw "A tarefa $leaf mudou desde a aplicação; ela foi preservada." }
        }

        function Restore-TrackedTask($Entry) {
            Assert-TaskUnchanged $Entry
            $folder, $leaf = Split-TaskPath ([string]$Entry.Path)
            Enable-ScheduledTask -TaskPath $folder -TaskName $leaf -ErrorAction Stop | Out-Null
            $after = Get-ScheduledTask -TaskPath $folder -TaskName $leaf -ErrorAction Stop
            if ([string]$after.State -eq 'Disabled') { throw "A tarefa $leaf continuou desativada após a reversão." }
        }

        function Undo-Tracker($Tracker) {
            $errors = New-Object System.Collections.Generic.List[string]
            $tasks = ConvertTo-Array $Tracker.Tasks
            $services = ConvertTo-Array $Tracker.Services
            $registry = ConvertTo-Array $Tracker.Registry
            for ($i = $tasks.Count - 1; $i -ge 0; $i--) { try { Restore-TrackedTask $tasks[$i] } catch { $errors.Add($_.Exception.Message) } }
            for ($i = $services.Count - 1; $i -ge 0; $i--) { try { Restore-TrackedService $services[$i] } catch { $errors.Add($_.Exception.Message) } }
            for ($i = $registry.Count - 1; $i -ge 0; $i--) { try { Restore-RegistryEntry $registry[$i] } catch { $errors.Add($_.Exception.Message) } }
            return ,$errors
        }

        function ConvertFrom-RollbackData([string]$Text) {
            $parsed = ConvertFrom-Json -InputObject $Text
            if ($parsed -is [array]) {
                # Formato antigo: lista de valores HKCU.
                return [pscustomobject]@{ Registry = @($parsed); Services = @(); Tasks = @() }
            }
            return [pscustomobject]@{
                Registry = (ConvertTo-Array $parsed.Registry)
                Services = (ConvertTo-Array $parsed.Services)
                Tasks = (ConvertTo-Array $parsed.Tasks)
            }
        }

        function Publish-Progress {
            try {
                $document = [ordered]@{ Results = @($results.ToArray()) }
                [System.IO.File]::WriteAllText($ProgressPath, (ConvertTo-Json -InputObject $document -Compress -Depth 10), [System.Text.UTF8Encoding]::new($false))
            } catch { }
        }

        foreach ($operation in @($payload.Operations)) {
            $outcome = 'Falha ou não verificado'
            $message = ''
            $previousState = 'Não disponível'
            $rollbackData = $null
            $rollbackOutcome = 'Não disponível'
            try {
                switch ([string]$operation.Kind) {
                    'RestorePoint' {
                        $policy = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore' -Name DisableSR -ErrorAction SilentlyContinue
                        if ($policy -and [int]$policy.DisableSR -eq 1) { throw 'A política do computador bloqueia a Proteção do Sistema.' }
                        $before = @(Get-ComputerRestorePoint -ErrorAction Stop | Sort-Object SequenceNumber)
                        $previousState = if ($before.Count -gt 0) { "Último ponto anterior: #$($before[-1].SequenceNumber)" } else { 'Nenhum ponto anterior encontrado' }
                        $description = "LznTweaker $([Guid]::NewGuid().ToString('N'))"
                        Checkpoint-Computer -Description $description -RestorePointType MODIFY_SETTINGS -ErrorAction Stop
                        $after = @(Get-ComputerRestorePoint -ErrorAction Stop)
                        $created = $after | Where-Object { $_.Description -eq $description } | Select-Object -First 1
                        if ($null -eq $created) { throw 'O ponto não apareceu na consulta posterior; a criação não foi confirmada (o Windows pode limitar a um ponto a cada 24 horas).' }
                        $outcome = 'Sucesso verificado'
                        $message = "Ponto encontrado após a criação (sequência #$($created.SequenceNumber))."
                        $rollbackOutcome = 'Sem reversão automática confiável'
                    }
                    'Tracked' {
                        $tracker = New-Tracker
                        $notes = New-Object System.Collections.Generic.List[string]
                        try {
                            foreach ($change in (ConvertTo-Array $operation.RegistryChanges)) { Set-TrackedRegistryValue 'HKCU' $change $tracker }
                            foreach ($change in (ConvertTo-Array $operation.HklmRegistryChanges)) { Set-TrackedRegistryValue 'HKLM' $change $tracker }
                            foreach ($svcChange in (ConvertTo-Array $operation.ServiceChanges)) { Set-TrackedService $svcChange $tracker $notes }
                            foreach ($taskPath in (ConvertTo-Array $operation.ScheduledTasks)) { Disable-TrackedTask ([string]$taskPath) $tracker $notes }
                        } catch {
                            $failure = $_.Exception.Message
                            $undoErrors = Undo-Tracker $tracker
                            $rollbackOutcome = if ($undoErrors.Count -eq 0) { 'Alterações parciais revertidas após erro.' } else { "A reversão parcial também falhou: $($undoErrors -join ' | ')" }
                            throw $failure
                        }
                        $changed = $tracker.Registry.Count + $tracker.Services.Count + $tracker.Tasks.Count
                        $message = "Verificados: $($tracker.Registry.Count) valor(es) do Registro, $($tracker.Services.Count) serviço(s), $($tracker.Tasks.Count) tarefa(s) agendada(s)."
                        if ($notes.Count -gt 0) { $message += ' Observações: ' + ($notes -join '; ') + '.' }
                        $previousState = "$changed alteração(ões) com estado anterior capturado"
                        if ($changed -eq 0) {
                            $outcome = 'Nada a alterar'
                            $rollbackOutcome = 'Nenhuma alteração para reverter.'
                        } else {
                            $outcome = 'Sucesso verificado'
                            $snapshot = [pscustomobject][ordered]@{
                                Registry = @($tracker.Registry.ToArray()); Services = @($tracker.Services.ToArray()); Tasks = @($tracker.Tasks.ToArray())
                            }
                            $rollbackData = ConvertTo-Json -InputObject $snapshot -Compress -Depth 6
                            $rollbackOutcome = 'Reversão registrada; só será aplicada se os valores ainda forem os deste app.'
                        }
                    }
                    'DeleteTempFiles' {
                        $root = [System.IO.Path]::GetFullPath([string]$operation.TempRoot).TrimEnd('\')
                        $actualRoot = [System.IO.Path]::GetFullPath($env:TEMP).TrimEnd('\')
                        if (-not [string]::Equals($root, $actualRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'A pasta TEMP mudou desde a prévia; nenhum arquivo foi removido.' }
                        $prefix = $root + '\'
                        $removed = 0; $skipped = 0; $bytesRemoved = [int64]0
                        foreach ($file in @($operation.Files)) {
                            try {
                                $path = [System.IO.Path]::GetFullPath([string]$file.FullPath)
                                if (-not $path.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) { $skipped++; continue }
                                if (-not [System.IO.File]::Exists($path)) { $skipped++; continue }
                                $info = [System.IO.FileInfo]::new($path)
                                if (($info.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
                                    $info.Length -ne [int64]$file.Length -or $info.LastWriteTimeUtc.Ticks -ne [int64]$file.LastWriteTimeUtcTicks) { $skipped++; continue }
                                $probe = [System.IO.File]::Open($path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::None)
                                $probe.Dispose()
                                [System.IO.File]::Delete($path)
                                if ([System.IO.File]::Exists($path)) { $skipped++; continue }
                                $removed++; $bytesRemoved += [int64]$file.Length
                            } catch { $skipped++ }
                        }
                        $fileCount = @($operation.Files).Count
                        $previousState = "$fileCount arquivo(s) selecionado(s) na prévia"
                        $outcome = if ($removed -gt 0) { 'Sucesso verificado' } else { 'Nenhum arquivo removido' }
                        $message = "Removidos: $removed; ignorados, ausentes, alterados ou em uso: $skipped; espaço removido: $bytesRemoved bytes."
                        $rollbackOutcome = 'Exclusão de temporários não possui reversão confiável.'
                    }
                    'CustomScript' {
                        $previousState = 'Estado anterior não capturado (script de sistema)'
                        $rollbackOutcome = 'Sem reversão automática pelo app.'
                        $global:LASTEXITCODE = 0
                        & ([scriptblock]::Create([string]$operation.InvokeScript)) | Out-Null
                        if ($LASTEXITCODE -ne 0) { throw "O comando terminou com código $LASTEXITCODE." }
                        if ($operation.VerifyScript) {
                            $verified = @(& ([scriptblock]::Create([string]$operation.VerifyScript))) | Select-Object -Last 1
                            if ($verified -ne $true) { throw 'O comando terminou, mas a verificação posterior não confirmou o novo estado.' }
                            $outcome = 'Sucesso verificado'
                            $message = 'Comando executado e novo estado confirmado pela verificação posterior.'
                        } else {
                            $outcome = 'Executado sem verificação'
                            $message = 'Comando executado sem erro; o app não consegue confirmar o efeito real desta operação.'
                        }
                    }
                    'Undo' {
                        $tracker = ConvertFrom-RollbackData ([string]$operation.RollbackData)
                        foreach ($entry in $tracker.Registry) { Assert-RegistryEntryUnchanged $entry }
                        foreach ($entry in $tracker.Services) { Assert-ServiceUnchanged $entry }
                        foreach ($entry in $tracker.Tasks) { Assert-TaskUnchanged $entry }
                        $undoErrors = Undo-Tracker $tracker
                        if ($undoErrors.Count -gt 0) { throw "Reversão incompleta: $($undoErrors -join ' | ')" }
                        $total = $tracker.Registry.Count + $tracker.Services.Count + $tracker.Tasks.Count
                        $previousState = "$total item(ns) verificado(s) antes da reversão"
                        $outcome = 'Reversão verificada'
                        $message = "Restaurados: $($tracker.Registry.Count) valor(es) do Registro, $($tracker.Services.Count) serviço(s), $($tracker.Tasks.Count) tarefa(s)."
                        $rollbackOutcome = 'Estado anterior restaurado.'
                    }
                    default { throw "Tipo de operação não permitido: $($operation.Kind)." }
                }
            } catch {
                $message = $_.Exception.Message
            }
            $results.Add([pscustomobject]@{
                TweakId = [string]$operation.TweakId; Outcome = $outcome; Message = $message
                PreviousState = $previousState; RollbackData = $rollbackData; RollbackOutcome = $rollbackOutcome
            })
            Publish-Progress
        }

        try {
            $document = [ordered]@{ Results = @($results.ToArray()) }
            [System.IO.File]::WriteAllText($ResultPath, (ConvertTo-Json -InputObject $document -Compress -Depth 10), [System.Text.UTF8Encoding]::new($false))
        } catch { [Console]::Error.WriteLine($_.Exception.Message); exit 2 }
        $okOutcomes = @('Sucesso verificado', 'Reversão verificada', 'Executado sem verificação', 'Nada a alterar')
        if (@($results | Where-Object { $_.Outcome -notin $okOutcomes }).Count -gt 0) { exit 1 }
        exit 0
        """;
}
