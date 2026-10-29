using System.IO;

namespace LznTweaker.Core.Execution;

/// <summary>Scripts PowerShell embutidos no assembly (pasta Execution/Scripts).</summary>
internal static class EmbeddedScripts
{
    private static readonly Lazy<string> BatchEngineScript = new(() => Read("BatchEngine.ps1"));
    private static readonly Lazy<string> EnableSystemProtectionScript = new(() => Read("EnableSystemProtection.ps1"));
    private static readonly Lazy<string> AppxInventoryScript = new(() => Read("AppxInventory.ps1"));

    /// <summary>Inventário somente leitura dos pacotes Appx.</summary>
    public static string AppxInventory => AppxInventoryScript.Value;

    /// <summary>Motor do lote de tweaks: aplicação rastreada, scripts verificados, limpeza de TEMP e Undo.</summary>
    public static string BatchEngine => BatchEngineScript.Value;

    /// <summary>Ativa a Proteção do Sistema no volume do Windows.</summary>
    public static string EnableSystemProtection => EnableSystemProtectionScript.Value;

    private static string Read(string fileName)
    {
        var resourceName = $"LznTweaker.Core.Execution.Scripts.{fileName}";
        using var stream = typeof(EmbeddedScripts).Assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException($"Script embutido não encontrado: {resourceName}");
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
