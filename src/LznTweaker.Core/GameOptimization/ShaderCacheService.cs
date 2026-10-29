using System.IO;
using LznTweaker.Core.Tweaks;

namespace LznTweaker.Core.GameOptimization;

public sealed record ShaderCacheLocation(string Id, string Label, string Vendor, string Path, long Bytes, int Files);

/// <summary>
/// Caches de shaders do DirectX e dos drivers de GPU (afetam todos os jogos). Só pastas conhecidas são aceitas;
/// a pasta em si é mantida e apenas o conteúdo é apagado. Não há backup: é cache e o jogo/driver o recria.
/// </summary>
public sealed class ShaderCacheService
{
    public const string OperationId = "shader-cache-clean";
    private readonly TweakOperationHistory _history;
    private readonly string _localAppData;
    private readonly string _localLow;

    public ShaderCacheService(TweakOperationHistory history)
        : this(history,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow")) { }

    internal ShaderCacheService(TweakOperationHistory history, string localAppData, string localLow)
    {
        _history = history;
        _localAppData = localAppData;
        _localLow = localLow;
    }

    private IEnumerable<(string Id, string Label, string Vendor, string Path)> Candidates()
    {
        yield return ("d3d", "Cache de shaders do DirectX", "Windows", Path.Combine(_localAppData, "D3DSCache"));
        yield return ("nvidia-dx", "NVIDIA DXCache", "NVIDIA", Path.Combine(_localAppData, "NVIDIA", "DXCache"));
        yield return ("nvidia-gl", "NVIDIA GLCache", "NVIDIA", Path.Combine(_localAppData, "NVIDIA", "GLCache"));
        yield return ("nvidia-low-dx", "NVIDIA DXCache (LocalLow, por versão do driver)", "NVIDIA", Path.Combine(_localLow, "NVIDIA", "PerDriverVersion", "DXCache"));
        yield return ("nvidia-low-gl", "NVIDIA GLCache (LocalLow, por versão do driver)", "NVIDIA", Path.Combine(_localLow, "NVIDIA", "PerDriverVersion", "GLCache"));
        yield return ("amd-dx", "AMD DxCache", "AMD", Path.Combine(_localAppData, "AMD", "DxCache"));
        yield return ("amd-dxc", "AMD DxcCache", "AMD", Path.Combine(_localAppData, "AMD", "DxcCache"));
        yield return ("amd-vk", "AMD VkCache (Vulkan)", "AMD", Path.Combine(_localAppData, "AMD", "VkCache"));
        yield return ("amd-gl", "AMD GLCache (OpenGL)", "AMD", Path.Combine(_localAppData, "AMD", "GLCache"));
    }

    /// <summary>Locais existentes, com tamanho e número de arquivos.</summary>
    public Task<IReadOnlyList<ShaderCacheLocation>> ScanAsync(CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<ShaderCacheLocation>>(() =>
        Candidates()
            .Where(candidate => Directory.Exists(candidate.Path))
            .Select(candidate =>
            {
                var (bytes, files) = Measure(candidate.Path, cancellationToken);
                return new ShaderCacheLocation(candidate.Id, candidate.Label, candidate.Vendor, Path.GetFullPath(candidate.Path), bytes, files);
            })
            .ToArray(), cancellationToken);

    public async Task<TweakOperationResult> CleanAsync(IReadOnlyList<ShaderCacheLocation> locations, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.Now;
        var allowed = Candidates().ToDictionary(candidate => candidate.Id, candidate => Path.GetFullPath(candidate.Path), StringComparer.Ordinal);
        var deleted = 0;
        var skipped = 0;
        long freed = 0;
        var cleaned = new List<string>();
        await Task.Run(() =>
        {
            foreach (var location in locations)
            {
                // Defesa: só limpa o local se ele for exatamente um dos caminhos conhecidos.
                if (!allowed.TryGetValue(location.Id, out var path) || !string.Equals(path, location.Path, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(path))
                    continue;
                if ((new DirectoryInfo(path).Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;
                var (d, s, b) = DeleteContents(path, cancellationToken);
                deleted += d;
                skipped += s;
                freed += b;
                cleaned.Add(location.Label);
            }
        }, cancellationToken);

        var outcome = cleaned.Count == 0 ? "Nada a alterar" : "Sucesso verificado";
        var result = new TweakOperationResult(Guid.NewGuid(), OperationId, startedAt,
            $"{locations.Count} local(is) selecionado(s)", "Limpar caches de shaders", outcome,
            cleaned.Count == 0
                ? "Nenhum dos locais selecionados existe mais."
                : $"Limpos: {string.Join(", ", cleaned)}. Removidos {deleted:N0} arquivo(s), {FormatBytes(freed)} liberados; {skipped:N0} em uso foram mantidos. " +
                  "Os jogos vão recompilar shaders e podem travar por alguns instantes nas primeiras partidas.",
            RequiresRestart: false, RollbackMethod: null, RollbackOutcome: "Sem reversão: é cache, recriado automaticamente pelos jogos e drivers.");
        await _history.AppendAsync(result, cancellationToken);
        return result;
    }

    internal static (long Bytes, int Files) Measure(string path, CancellationToken cancellationToken)
    {
        long bytes = 0;
        var files = 0;
        foreach (var file in EnumerateFiles(path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { bytes += new FileInfo(file).Length; files++; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return (bytes, files);
    }

    private static (int Deleted, int Skipped, long Bytes) DeleteContents(string root, CancellationToken cancellationToken)
    {
        var deleted = 0;
        var skipped = 0;
        long bytes = 0;
        foreach (var file in EnumerateFiles(root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var length = new FileInfo(file).Length;
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
                deleted++;
                bytes += length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                skipped++; // em uso pelo driver ou por um jogo aberto
            }
        }
        // Remove subpastas que ficaram vazias, mantendo a pasta raiz.
        foreach (var directory in Directory.EnumerateDirectories(root, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint })
                     .OrderByDescending(directory => directory.Length))
        {
            try { if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return (deleted, skipped, bytes);
    }

    private static IEnumerable<string> EnumerateFiles(string path) => Directory.EnumerateFiles(path, "*", new EnumerationOptions
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint
    });

    public static string FormatBytes(long bytes) => bytes >= 1_073_741_824
        ? $"{bytes / 1_073_741_824d:0.0} GB"
        : bytes >= 1_048_576 ? $"{bytes / 1_048_576d:0} MB" : $"{bytes / 1024d:0} KB";
}
