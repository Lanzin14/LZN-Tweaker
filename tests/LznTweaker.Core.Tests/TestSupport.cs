using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Principal;
using LznTweaker.Core.Execution;
using Microsoft.Win32;

namespace LznTweaker.Core.Tests;

/// <summary>
/// Teste que só roda sem elevação. Elevado, o motor criaria pontos de restauração e alteraria HKLM de verdade,
/// então o teste é pulado em vez de mexer no sistema.
/// </summary>
public sealed class NonElevatedFactAttribute : FactAttribute
{
    public NonElevatedFactAttribute()
    {
        if (ProcessElevation.IsElevated)
            Skip = "Rode os testes sem elevação: elevado, este teste alteraria o sistema real.";
    }
}

/// <summary>Executa o BatchEngine.ps1 real por meio do ScriptRunnerService, em um espaço de execução temporário.</summary>
internal static class BatchEngineHarness
{
    public static readonly string UserSid = WindowsIdentity.GetCurrent().User!.Value;

    public static ScriptRunnerService CreateRunner() =>
        new(new ElevatedWorkspace(Path.Combine(Path.GetTempPath(), "LznTweakerTests", "Operations"), restrictToAdministrators: false),
            allowElevationPrompt: false);

    public static JsonArray Run(object operations, bool requireRestorePoint = false)
    {
        var payload = JsonSerializer.Serialize(new { Operations = operations, UserSid, RequireRestorePoint = requireRestorePoint });
        var result = CreateRunner()
            .RunAsync(new ScriptRunRequest("test", EmbeddedScripts.BatchEngine, payload, TimeSpan.FromMinutes(2), RequiresElevation: false))
            .GetAwaiter().GetResult();
        Assert.True(result.ResultJson is not null, $"Sem resultado: {result.Message} {result.ErrorOutput}");
        return (JsonArray)JsonNode.Parse(result.ResultJson!)!["Results"]!;
    }
}

/// <summary>Chave isolada HKCU\Software\LznTweakerTest_{guid}, removida ao final de cada teste.</summary>
internal sealed class SandboxKey : IDisposable
{
    public SandboxKey()
    {
        Path = $@"Software\LznTweakerTest_{Guid.NewGuid():N}";
        using var key = Registry.CurrentUser.CreateSubKey(Path);
        key.SetValue("DwordVal", 1, RegistryValueKind.DWord);
        key.SetValue("StrVal", "510", RegistryValueKind.String);
        key.SetValue("BinVal", new byte[] { 1, 2 }, RegistryValueKind.Binary);
    }

    public string Path { get; }

    public object Change(string name, RegistryValueKind kind, object value) =>
        new { SubKey = Path, Name = name, Kind = (int)kind, Value = value };

    public object? Read(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(Path);
        return key?.GetValue(name);
    }

    public RegistryValueKind? KindOf(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(Path);
        return key is not null && key.GetValueNames().Contains(name) ? key.GetValueKind(name) : null;
    }

    public void Write(string name, object value, RegistryValueKind kind)
    {
        using var key = Registry.CurrentUser.OpenSubKey(Path, writable: true)!;
        key.SetValue(name, value, kind);
    }

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(Path, throwOnMissingSubKey: false);
}
