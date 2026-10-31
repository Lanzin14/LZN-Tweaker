using System.IO;
using System.Text.Json;

namespace LznTweaker.Core.Preferences;

/// <summary>Escolhas de interface do usuário (não alteram nada no sistema).</summary>
public sealed record UiPreferences
{
    /// <summary>Sidebar só com ícones. Padrão: recolhida (layout Foco).</summary>
    public bool SidebarCollapsed { get; init; } = true;
}

/// <summary>
/// Guarda <see cref="UiPreferences"/> em %LocalAppData%\LznTweaker\ui.json. Arquivo ausente ou ilegível volta ao padrão;
/// o ilegível é preservado com sufixo .invalido. Falha ao salvar não interrompe o app (a preferência só não é lembrada).
/// </summary>
public sealed class UiPreferencesStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path;

    public UiPreferencesStore()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LznTweaker", "ui.json")) { }

    /// <summary>Caminho explícito (testes usam uma pasta temporária para não tocar nas preferências reais).</summary>
    public UiPreferencesStore(string path) => _path = path;

    public string FilePath => _path;

    public UiPreferences Load()
    {
        if (!File.Exists(_path))
            return new UiPreferences();
        try
        {
            return JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(_path), Options) ?? new UiPreferences();
        }
        catch (JsonException)
        {
            try { File.Copy(_path, _path + ".invalido", overwrite: true); } catch (IOException) { }
            return new UiPreferences();
        }
    }

    public bool TrySave(UiPreferences preferences)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(preferences, Options));
            File.Move(temp, _path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
