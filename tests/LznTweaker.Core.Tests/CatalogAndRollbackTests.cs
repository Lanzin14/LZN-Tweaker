using System.Text.Json;
using LznTweaker.Core.Tweaks;
using Microsoft.Win32;

namespace LznTweaker.Core.Tests;

/// <summary>Validação local do histórico, catálogo, presets e conflitos (sem executar nada no sistema).</summary>
public sealed class CatalogAndRollbackTests
{
    private static bool IsValid(string tweakId, string rollbackData) => TweakBatchExecutor.IsCatalogRollbackData(
        new TweakOperationResult(Guid.NewGuid(), tweakId, DateTimeOffset.Now, "", "", "Sucesso verificado", "", false, null, null, rollbackData));

    private static string Entry(string hive, RegistryValueChange change) => JsonSerializer.Serialize(new
    {
        Hive = hive, change.SubKey, change.Name, OldExists = true,
        OldValue = change.Kind == RegistryValueKind.String ? (object)"1" : 1,
        OldKind = (int)change.Kind, AppliedValue = change.Value, AppliedKind = (int)change.Kind
    });

    private static string MouseData()
    {
        var mouse = TweakCatalog.GetById(TweakCatalog.DisableMouseAccelerationId);
        return "{\"Registry\":[" + string.Join(",", mouse.RegistryChanges!.Select(change => Entry("HKCU", change))) + "],\"Services\":[],\"Tasks\":[]}";
    }

    private static string TelemetryData()
    {
        var telemetry = TweakCatalog.GetById(TweakCatalog.DisableTelemetryId);
        return "{\"Registry\":[" + string.Join(",", telemetry.HklmRegistryChanges!.Select(change => Entry("HKLM", change))) + "]," +
               "\"Services\":[{\"Name\":\"DiagTrack\",\"OldStart\":2,\"OldDelayed\":0,\"AppliedStart\":4}]," +
               "\"Tasks\":[{\"Path\":\"" + telemetry.ScheduledTasksToDisable![0].Replace("\\", "\\\\") + "\"}]}";
    }

    [Fact]
    public void String_rollback_is_accepted() => Assert.True(IsValid(TweakCatalog.DisableMouseAccelerationId, MouseData()));

    [Fact]
    public void Hklm_service_and_task_rollback_is_accepted() => Assert.True(IsValid(TweakCatalog.DisableTelemetryId, TelemetryData()));

    [Theory]
    [InlineData("\"DiagTrack\"", "\"WinDefend\"")]
    [InlineData("Microsoft Compatibility Appraiser", "Outra")]
    [InlineData("\"HKLM\"", "\"HKCU\"")]
    public void Tampered_telemetry_rollback_is_refused(string original, string tampered) =>
        Assert.False(IsValid(TweakCatalog.DisableTelemetryId, TelemetryData().Replace(original, tampered)));

    [Fact]
    public void Applied_value_different_from_catalog_is_refused() =>
        Assert.False(IsValid(TweakCatalog.DisableMouseAccelerationId, MouseData().Replace("\"AppliedValue\":\"0\"", "\"AppliedValue\":\"7\"")));

    [Fact]
    public void Legacy_array_with_hklm_is_refused()
    {
        var telemetry = TweakCatalog.GetById(TweakCatalog.DisableTelemetryId);
        Assert.False(IsValid(telemetry.Id, "[" + string.Join(",", telemetry.HklmRegistryChanges!.Select(change => Entry("HKLM", change))) + "]"));
    }

    [Fact]
    public void Invalid_json_is_refused() => Assert.False(IsValid(TweakCatalog.DisableMouseAccelerationId, "{nope"));

    [Fact]
    public void Every_catalog_definition_builds_an_executable_operation()
    {
        foreach (var definition in TweakCatalog.All)
            TweakBatchExecutor.ToScriptOperation(definition, []);
    }

    [Fact]
    public void Script_based_tweaks_never_claim_reliable_rollback() =>
        Assert.All(TweakCatalog.All.Where(definition => definition.InvokeScript is not null), definition => Assert.False(definition.HasReliableRollback));

    [Theory]
    [InlineData(TweakPreset.Minimal)]
    [InlineData(TweakPreset.Standard)]
    [InlineData(TweakPreset.Advanced)]
    public void Preset_ids_exist_and_round_trip(TweakPreset preset)
    {
        var ids = TweakCatalog.All.Select(definition => definition.Id).ToHashSet();
        var selection = TweakPresetCatalog.GetSelection(preset);
        Assert.All(selection, id => Assert.Contains(id, ids));
        Assert.Equal(preset, TweakPresetCatalog.MatchImplementedSelection(selection));
    }

    [Fact]
    public void Standard_matches_the_implemented_part_of_winutil_standard() =>
        Assert.True(TweakPresetCatalog.GetSelection(TweakPreset.Standard).SetEquals(
        [
            TweakCatalog.CreateRestorePointId, TweakCatalog.DisableActivityHistoryId, TweakCatalog.DisableConsumerFeaturesId,
            TweakCatalog.DisableLocationTrackingId, TweakCatalog.DisableTelemetryId, TweakCatalog.DeleteTemporaryFilesId,
            TweakCatalog.EndTaskOnTaskbarId
        ]));

    [Fact]
    public void No_preset_selects_preferences_hibernation_or_ip_changes() =>
        Assert.All(new[] { TweakPreset.Minimal, TweakPreset.Standard, TweakPreset.Advanced }.SelectMany(TweakPresetCatalog.GetSelection), id =>
        {
            Assert.NotEqual(TweakCategory.CustomizePreferences, TweakCatalog.GetById(id).Category);
            Assert.DoesNotContain(id, new[] { TweakCatalog.DisableHibernationId, TweakCatalog.SetIpv4PriorityId, TweakCatalog.DisableIpv6Id });
        });

    [Fact]
    public void Conflicting_pairs_are_detected_and_independent_items_are_not()
    {
        var found = TweakBatchExecutor.GetConflictingIds(
        [
            TweakCatalog.GetById(TweakCatalog.SetIpv4PriorityId),
            TweakCatalog.GetById(TweakCatalog.DisableIpv6Id),
            TweakCatalog.GetById(TweakCatalog.DarkThemeId)
        ]);
        Assert.Equal(2, found.Count);
        Assert.False(found.ContainsKey(TweakCatalog.DarkThemeId));
    }

    [Fact]
    public void Restore_point_is_always_first_and_never_duplicated()
    {
        var restorePoint = TweakCatalog.GetById(TweakCatalog.CreateRestorePointId);
        var darkTheme = TweakCatalog.GetById(TweakCatalog.DarkThemeId);

        var withoutIt = TweakBatchExecutor.WithMandatoryRestorePoint([darkTheme]);
        var withItLast = TweakBatchExecutor.WithMandatoryRestorePoint([darkTheme, restorePoint]);

        Assert.Equal([TweakCatalog.CreateRestorePointId, TweakCatalog.DarkThemeId], withoutIt.Select(definition => definition.Id));
        Assert.Equal([TweakCatalog.CreateRestorePointId, TweakCatalog.DarkThemeId], withItLast.Select(definition => definition.Id));
        Assert.Empty(TweakBatchExecutor.WithMandatoryRestorePoint([]));
    }
}
