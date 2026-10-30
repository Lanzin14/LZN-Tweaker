using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LznTweaker.Core.Gpu;

/// <summary>Resposta do próprio usuário sobre o monitor: o Windows não informa de forma confiável se G-SYNC/FreeSync está ativo.</summary>
public enum VrrAnswer { NotAnswered, Available, NotAvailable }

/// <summary>
/// Preferências da página Placa de Vídeo. Guarda só escolhas do usuário; nenhuma configuração de driver ou de jogo
/// é alterada a partir daqui. Arquivos antigos com os campos dos modos removidos ("Mode", "CustomValues") ainda
/// carregam: campos desconhecidos são ignorados.
/// </summary>
public sealed record GpuPreferences
{
    public VrrAnswer Vrr { get; init; } = VrrAnswer.NotAnswered;
}

public sealed class GpuPreferencesStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;

    public GpuPreferencesStore()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LznTweaker", "gpu.json")) { }

    internal GpuPreferencesStore(string path) => _path = path;

    public string FilePath => _path;

    /// <summary>Arquivo ausente começa vazio. Arquivo ilegível também, mas é preservado com sufixo .invalido para não se perder.</summary>
    public GpuPreferences Load()
    {
        if (!File.Exists(_path))
            return new GpuPreferences();
        try
        {
            return JsonSerializer.Deserialize<GpuPreferences>(File.ReadAllText(_path), Options) ?? new GpuPreferences();
        }
        catch (JsonException)
        {
            try { File.Copy(_path, _path + ".invalido", overwrite: true); } catch (IOException) { }
            return new GpuPreferences();
        }
    }

    public void Save(GpuPreferences preferences)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(preferences, Options));
        File.Move(temp, _path, overwrite: true);
    }
}
