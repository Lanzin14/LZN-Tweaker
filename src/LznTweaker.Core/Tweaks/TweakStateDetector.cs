using System.IO;
using System.Security;
using Microsoft.Win32;

namespace LznTweaker.Core.Tweaks;

/// <summary>
/// Lê o estado real de um ajuste do catálogo (Registro e tipo de inicialização de serviços).
/// Ajustes feitos por script não têm detecção direta e retornam Unknown, sem adivinhar.
/// </summary>
public static class TweakStateDetector
{
    public static (TweakSystemState State, string Detail) Detect(TweakDefinition definition)
    {
        if (definition.Id == TweakCatalog.EndTaskOnTaskbarId && !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            return (TweakSystemState.Unsupported, "requer Windows 11; nada foi alterado.");

        var allChanges = new List<(string SubKey, string Name, object Value, bool IsHklm)>();
        foreach (var service in definition.ServiceChanges ?? [])
        {
            // Serviços ausentes nesta versão do Windows não entram no cálculo do estado.
            using var serviceKey = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{service.Name}", writable: false);
            if (serviceKey is not null)
                allChanges.Add(($@"SYSTEM\CurrentControlSet\Services\{service.Name}", "Start", ServiceStartCode(service.TargetStartupType), true));
        }
        foreach (var change in definition.RegistryChanges ?? [])
            allChanges.Add((change.SubKey, change.Name, change.Value, false));
        foreach (var change in definition.HklmRegistryChanges ?? [])
            allChanges.Add((change.SubKey, change.Name, change.Value, true));

        if (allChanges.Count == 0)
        {
            if (definition.Id == TweakCatalog.ActivateUltimatePerformanceId)
                return (TweakSystemState.Unknown, "Plano de energia de alta performance.");
            if (definition.Id == TweakCatalog.RemoveUltimatePerformanceId)
                return (TweakSystemState.Unknown, "Restaura o plano Equilibrado.");
            return (TweakSystemState.Unknown, "Nenhum método de detecção direta no Registro.");
        }

        try
        {
            var states = allChanges.Select(change =>
            {
                using var key = change.IsHklm
                    ? Registry.LocalMachine.OpenSubKey(change.SubKey, writable: false)
                    : Registry.CurrentUser.OpenSubKey(change.SubKey, writable: false);
                if (key is null || !key.GetValueNames().Contains(change.Name, StringComparer.OrdinalIgnoreCase))
                    return (Known: false, Matches: false);
                var value = key.GetValue(change.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                return (Known: true, Matches: string.Equals(Convert.ToString(value), Convert.ToString(change.Value), StringComparison.OrdinalIgnoreCase));
            }).ToArray();

            if (states.All(state => state.Known && state.Matches))
                return (TweakSystemState.Enabled, "todos os valores esperados estão aplicados.");
            if (states.All(state => state.Known && !state.Matches))
                return (TweakSystemState.Disabled, "os valores atuais diferem dos que este ajuste aplica.");
            if (states.Any(state => state.Known && state.Matches))
                return (TweakSystemState.Mixed, "valores parciais encontrados.");
            return (TweakSystemState.Unknown, "valores ausentes ou não configurados.");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
        {
            return (TweakSystemState.Unknown, $"leitura do Registro falhou: {ex.Message}");
        }
    }

    /// <summary>Última aplicação reversível de cada ajuste do catálogo que ainda não foi desfeita.</summary>
    public static IReadOnlyDictionary<string, TweakOperationResult> GetUndoCandidates(IReadOnlyList<TweakOperationResult> entries)
    {
        var catalogIds = TweakCatalog.All.Select(definition => definition.Id).ToHashSet(StringComparer.Ordinal);
        var candidates = new Dictionary<string, TweakOperationResult>(StringComparer.Ordinal);
        foreach (var entry in entries.Where(entry => catalogIds.Contains(entry.TweakId)))
        {
            if (entry.RequestedState == "Desfazer")
            {
                if (entry.Outcome == "Reversão verificada")
                    candidates.Remove(entry.TweakId);
                continue;
            }

            if (entry.Outcome == "Sucesso verificado" && !string.IsNullOrWhiteSpace(entry.RollbackData))
                candidates[entry.TweakId] = entry;
        }
        return candidates;
    }

    private static int ServiceStartCode(string startupType) => startupType switch
    {
        "Automatic" => 2,
        "Manual" => 3,
        _ => 4
    };
}
