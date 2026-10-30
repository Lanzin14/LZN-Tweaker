using System.IO;
using LznTweaker.Core.Cleanup;
using LznTweaker.Core.Tweaks;

namespace LznTweaker.Core.Tests;

/// <summary>Limpeza em uma pasta temporária falsa: nada fora dela é tocado.</summary>
public sealed class CleanupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"LznTweakerTest_{Guid.NewGuid():N}");
    private readonly string _temp;
    private readonly string _outside;
    private readonly SystemCleanupService _service;

    public CleanupTests()
    {
        _temp = Directory.CreateDirectory(Path.Combine(_root, "temp")).FullName;
        _outside = Directory.CreateDirectory(Path.Combine(_root, "fora")).FullName;
        _service = new SystemCleanupService(category => category == CleanupCategoryType.UserTemp ? _temp : null);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string CreateFile(string folder, string name, TimeSpan age, int bytes = 10)
    {
        var path = Path.Combine(folder, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow - age);
        return path;
    }

    [Fact]
    public async Task Scan_lists_old_files_recursively_and_skips_recent_ones()
    {
        CreateFile(_temp, "velho.tmp", TimeSpan.FromHours(2), 100);
        CreateFile(_temp, Path.Combine("sub", "velho2.tmp"), TimeSpan.FromDays(3), 50);
        CreateFile(_temp, "recente.tmp", TimeSpan.FromMinutes(5));

        var result = await _service.ScanCategoryAsync(CleanupCategoryType.UserTemp);

        Assert.Equal(["velho.tmp", "velho2.tmp"], result.Files.Select(f => f.DisplayName).Order());
        Assert.Equal(150, result.TotalBytes);
    }

    [Fact]
    public async Task Missing_folder_is_reported_without_files()
    {
        var result = await _service.ScanCategoryAsync(CleanupCategoryType.SystemLogs);
        Assert.Empty(result.Files);
        Assert.NotNull(result.WarningMessage);
    }

    [Fact]
    public async Task Clean_deletes_only_selected_files_and_keeps_files_in_use()
    {
        var selected = CreateFile(_temp, "apagar.tmp", TimeSpan.FromHours(1), 40);
        var inUse = CreateFile(_temp, "em-uso.tmp", TimeSpan.FromHours(1), 30);
        var notSelected = CreateFile(_temp, "manter.tmp", TimeSpan.FromHours(1));
        var scan = await _service.ScanCategoryAsync(CleanupCategoryType.UserTemp);
        var toClean = scan.Files.Where(f => f.DisplayName is "apagar.tmp" or "em-uso.tmp").ToArray();

        CleanupExecutionSummary summary;
        using (File.Open(inUse, FileMode.Open, FileAccess.Read, FileShare.Read))
            summary = await _service.CleanFilesAsync(toClean, emptyRecycleBin: false);

        Assert.False(File.Exists(selected));
        Assert.True(File.Exists(inUse));
        Assert.True(File.Exists(notSelected));
        Assert.Equal(1, summary.TotalDeleted);
        Assert.Equal(1, summary.TotalSkipped);
        Assert.Equal(40, summary.TotalBytesFreed);
    }

    [Fact]
    public async Task Clean_refuses_files_outside_the_category_folder()
    {
        var outside = CreateFile(_outside, "importante.txt", TimeSpan.FromDays(1));
        // Também cobre um caminho com ".." que aparenta estar dentro da pasta.
        var sneaky = Path.Combine(_temp, "..", "fora", "importante.txt");
        var items = new[]
        {
            new CleanupFileItem(outside, 10, DateTime.Now, CleanupCategoryType.UserTemp),
            new CleanupFileItem(sneaky, 10, DateTime.Now, CleanupCategoryType.UserTemp),
            new CleanupFileItem(outside, 10, DateTime.Now, CleanupCategoryType.ShaderCache),
        };

        var summary = await _service.CleanFilesAsync(items, emptyRecycleBin: false);

        Assert.True(File.Exists(outside));
        Assert.Equal(0, summary.TotalDeleted);
        Assert.Equal(3, summary.TotalSkipped);
        Assert.All(summary.Errors, error => Assert.Contains("fora da pasta", error));
    }
}

public sealed class TweakStateTests
{
    private static TweakOperationResult Entry(string tweakId, string requested, string outcome, string? rollback, int minute) =>
        new(Guid.NewGuid(), tweakId, new DateTimeOffset(2026, 10, 1, 12, minute, 0, TimeSpan.Zero), "antes", requested, outcome, "", false,
            rollback is null ? null : "Tracked", null, rollback);

    [Fact]
    public void Undo_candidate_is_the_last_verified_apply_until_a_verified_undo()
    {
        var id = TweakCatalog.DisableGameDvrId;
        var first = Entry(id, "Aplicar", "Sucesso verificado", "{\"a\":1}", 1);
        var second = Entry(id, "Aplicar", "Sucesso verificado", "{\"a\":2}", 2);

        var candidates = TweakStateDetector.GetUndoCandidates([first, second]);
        Assert.Same(second, candidates[id]);

        var undone = TweakStateDetector.GetUndoCandidates([first, second, Entry(id, "Desfazer", "Reversão verificada", null, 3)]);
        Assert.False(undone.ContainsKey(id));
    }

    [Fact]
    public void Failed_or_unrecorded_applies_and_unknown_ids_are_not_undo_candidates()
    {
        var id = TweakCatalog.DisableGameDvrId;
        var candidates = TweakStateDetector.GetUndoCandidates(
        [
            Entry(id, "Aplicar", "Falha", "{\"a\":1}", 1),
            Entry(TweakCatalog.DisableTelemetryId, "Aplicar", "Sucesso verificado", null, 2),
            Entry("nao-existe", "Aplicar", "Sucesso verificado", "{\"a\":1}", 3),
            // Uma reversão que falhou não remove o candidato anterior.
            Entry(TweakCatalog.DisableRecallId, "Aplicar", "Sucesso verificado", "{\"b\":1}", 4),
            Entry(TweakCatalog.DisableRecallId, "Desfazer", "Falha", null, 5),
        ]);

        Assert.Equal([TweakCatalog.DisableRecallId], candidates.Keys);
    }

    [Fact]
    public void Script_only_tweaks_report_unknown_instead_of_guessing()
    {
        var (state, _) = TweakStateDetector.Detect(TweakCatalog.GetById(TweakCatalog.ActivateUltimatePerformanceId));
        Assert.Equal(TweakSystemState.Unknown, state);
    }
}
