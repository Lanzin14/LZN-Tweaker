using System.Text.Json;
using System.IO;

namespace LznTweaker.Core.Tweaks;

public sealed record TweakOperationResult(
    Guid OperationId,
    string TweakId,
    DateTimeOffset StartedAt,
    string PreviousState,
    string RequestedState,
    string Outcome,
    string Message,
    bool RequiresRestart,
    string? RollbackMethod,
    string? RollbackOutcome,
    string? RollbackData = null);

public sealed class TweakOperationHistory
{
    private static readonly SemaphoreSlim WriteLock = new(1, 1);
    private readonly string _historyPath;

    /// <param name="historyPath">Caminho do arquivo; o padrão é %LOCALAPPDATA%\LznTweaker\history.json.</param>
    public TweakOperationHistory(string? historyPath = null) =>
        _historyPath = historyPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LznTweaker", "history.json");

    public async Task AppendAsync(TweakOperationResult entry, CancellationToken cancellationToken = default)
    {
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_historyPath)!);
            var entries = await ReadEntriesAsync(cancellationToken);
            entries.Add(entry);

            var temporaryPath = _historyPath + ".tmp";
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
            File.Move(temporaryPath, _historyPath, overwrite: true);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public async Task<IReadOnlyList<TweakOperationResult>> ReadAsync(CancellationToken cancellationToken = default)
    {
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            return await ReadEntriesAsync(cancellationToken);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    private async Task<List<TweakOperationResult>> ReadEntriesAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_historyPath))
            return [];

        await using var stream = File.OpenRead(_historyPath);
        return await JsonSerializer.DeserializeAsync<List<TweakOperationResult>>(stream, cancellationToken: cancellationToken)
               ?? [];
    }
}
