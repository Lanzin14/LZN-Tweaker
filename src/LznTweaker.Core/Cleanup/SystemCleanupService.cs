using LznTweaker.Core.Tweaks;
using System.IO;
using System.Runtime.InteropServices;

namespace LznTweaker.Core.Cleanup;

public enum CleanupCategoryType
{
    UserTemp,
    SystemTemp,
    WindowsUpdateCache,
    SystemLogs,
    RecycleBin,
    ShaderCache
}

public sealed record CleanupCategoryInfo(
    CleanupCategoryType Type,
    string Title,
    string Description,
    string PathSummary,
    bool RequiresElevation = false);

public sealed record CleanupFileItem(
    string FullPath,
    long Length,
    DateTime LastWriteTime,
    CleanupCategoryType Category)
{
    public string DisplayName => Path.GetFileName(FullPath);
    public string DirectoryPath => Path.GetDirectoryName(FullPath) ?? string.Empty;
    public string SizeText => FormatBytes(Length);

    public static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824)
            return $"{bytes / 1_073_741_824d:0.00} GB";
        if (bytes >= 1_048_576)
            return $"{bytes / 1_048_576d:0.0} MB";
        if (bytes >= 1_024)
            return $"{bytes / 1_024d:0} KB";
        return $"{bytes} B";
    }
}

public sealed record CleanupScanResult(
    CleanupCategoryType Category,
    IReadOnlyList<CleanupFileItem> Files,
    long TotalBytes,
    string? WarningMessage = null)
{
    public string TotalSizeText => CleanupFileItem.FormatBytes(TotalBytes);
    public int FileCount => Files.Count;
}

public sealed record CleanupExecutionProgress(
    int TotalFiles,
    int ProcessedFiles,
    int DeletedFiles,
    int SkippedInUse,
    long BytesFreed,
    string CurrentItemName);

public sealed record CleanupExecutionSummary(
    int TotalProcessed,
    int TotalDeleted,
    int TotalSkipped,
    long TotalBytesFreed,
    IReadOnlyList<string> Errors);

public sealed class SystemCleanupService
{
    /// <summary>Prefixo do registro de cada limpeza no histórico (página Backups, área "Limpeza").</summary>
    public const string OperationPrefix = "cleanup:";

    // Pasta de cada categoria; os testes trocam por pastas temporárias.
    private readonly Func<CleanupCategoryType, string?> _folders;
    private readonly TweakOperationHistory? _history;

    public SystemCleanupService() : this(GetFolderPath) { }

    public SystemCleanupService(TweakOperationHistory history) : this(GetFolderPath, history) { }

    internal SystemCleanupService(Func<CleanupCategoryType, string?> folders, TweakOperationHistory? history = null)
    {
        _folders = folders;
        _history = history;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBinW(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBinW(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI   = 0x00000002;
    private const uint SHERB_NOSOUND        = 0x00000004;

    public static IReadOnlyList<CleanupCategoryInfo> GetSupportedCategories() =>
    [
        new(CleanupCategoryType.UserTemp, "Temporários do Usuário (%TEMP%)",
            "Arquivos temporários que os programas criam na sua conta. Os que estiverem em uso são mantidos.",
            Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath()),

        new(CleanupCategoryType.SystemTemp, "Temporários do Sistema (Windows Temp)",
            "Arquivos temporários de serviços do Windows e de instaladores.",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
            RequiresElevation: true),

        new(CleanupCategoryType.WindowsUpdateCache, "Cache do Windows Update",
            "Instaladores de atualizações que o Windows já aplicou (SoftwareDistribution\\Download).",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download"),
            RequiresElevation: true),

        new(CleanupCategoryType.SystemLogs, "Relatórios e Logs Residuais",
            "Relatórios de travamentos antigos. Só são úteis se você estiver investigando um erro.",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps")),

        new(CleanupCategoryType.ShaderCache, "Cache de Compilação de Shaders (DirectX)",
            "Shaders já preparados pelo DirectX. Os jogos recriam o cache, mas as primeiras partidas podem ter travadas.",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache")),

        new(CleanupCategoryType.RecycleBin, "Lixeira do Windows",
            "Arquivos que você já apagou. Esvaziar a Lixeira é definitivo.",
            "Todos os volumes do sistema")
    ];

    public async Task<CleanupScanResult> ScanCategoryAsync(CleanupCategoryType category, CancellationToken cancellationToken = default) =>
        await Task.Run(() => ScanCategory(category, cancellationToken), cancellationToken);

    private CleanupScanResult ScanCategory(CleanupCategoryType category, CancellationToken cancellationToken)
    {
        if (category == CleanupCategoryType.RecycleBin)
            return QueryRecycleBin();

        var targetFolder = _folders(category);
        if (string.IsNullOrWhiteSpace(targetFolder) || !Directory.Exists(targetFolder))
            return new CleanupScanResult(category, [], 0, "Diretório não encontrado ou inacessível.");

        var files = new List<CleanupFileItem>();
        long totalBytes = 0;
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false
        };

        try
        {
            foreach (var filePath in Directory.EnumerateFiles(targetFolder, "*", enumerationOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    // Pula arquivos recentemente modificados (menos de 30 minutos atrás) para segurança
                    if (DateTime.UtcNow - fileInfo.LastWriteTimeUtc < TimeSpan.FromMinutes(30))
                        continue;

                    files.Add(new CleanupFileItem(fileInfo.FullName, fileInfo.Length, fileInfo.LastWriteTime, category));
                    totalBytes += fileInfo.Length;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Arquivos com atributos especiais ou bloqueados na enumeração são ignorados
                }
            }
        }
        catch (Exception ex)
        {
            return new CleanupScanResult(category, files, totalBytes, $"Enumeração parcial: {ex.Message}");
        }

        return new CleanupScanResult(category, files, totalBytes);
    }

    private static CleanupScanResult QueryRecycleBin()
    {
        try
        {
            var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
            int hResult = SHQueryRecycleBinW(null, ref info);
            if (hResult == 0)
            {
                var dummyList = new List<CleanupFileItem>();
                if (info.i64NumItems > 0)
                {
                    dummyList.Add(new CleanupFileItem("Lixeira (Itens consolidados)", info.i64Size, DateTime.Now, CleanupCategoryType.RecycleBin));
                }
                return new CleanupScanResult(CleanupCategoryType.RecycleBin, dummyList, info.i64Size);
            }
        }
        catch
        {
            // Fallback caso a API nativa não responda
        }
        return new CleanupScanResult(CleanupCategoryType.RecycleBin, [], 0, "Não foi possível consultar a Lixeira.");
    }

    public async Task<CleanupExecutionSummary> CleanFilesAsync(
        IReadOnlyList<CleanupFileItem> filesToClean,
        bool emptyRecycleBin,
        IProgress<CleanupExecutionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.Now;
        var summary = await Task.Run(() =>
        {
            int processed = 0;
            int deleted = 0;
            int skipped = 0;
            long bytesFreed = 0;
            var errors = new List<string>();
            var regularFiles = filesToClean.Where(f => f.Category != CleanupCategoryType.RecycleBin).ToArray();

            foreach (var item in regularFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                processed++;

                try
                {
                    // Defesa: só apaga arquivos que estão dentro da pasta da própria categoria.
                    if (!IsInsideCategoryFolder(item))
                    {
                        skipped++;
                        errors.Add($"{item.DisplayName}: fora da pasta da categoria; não foi apagado.");
                    }
                    else if (File.Exists(item.FullPath))
                    {
                        // Teste de lock: verifica se o arquivo está sendo usado por outro processo
                        using (var stream = File.Open(item.FullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        {
                            // Se abriu com exclusividade, o arquivo está livre para remoção
                        }

                        File.SetAttributes(item.FullPath, FileAttributes.Normal);
                        File.Delete(item.FullPath);
                        deleted++;
                        bytesFreed += item.Length;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch (IOException)
                {
                    // Arquivo em uso por outro processo ativo - ignorado com segurança
                    skipped++;
                }
                catch (UnauthorizedAccessException)
                {
                    // Permissão restrita pelo Windows - ignorado com segurança
                    skipped++;
                }
                catch (Exception ex)
                {
                    skipped++;
                    errors.Add($"{item.DisplayName}: {ex.Message}");
                }

                progress?.Report(new CleanupExecutionProgress(
                    regularFiles.Length, processed, deleted, skipped, bytesFreed, item.DisplayName));
            }

            if (emptyRecycleBin)
            {
                var recycleBinBytes = filesToClean.Where(f => f.Category == CleanupCategoryType.RecycleBin).Sum(f => f.Length);
                try
                {
                    var hResult = SHEmptyRecycleBinW(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                    var after = QueryRecycleBin();
                    if (hResult < 0 && after.TotalBytes > 0)
                        errors.Add($"A Lixeira não foi esvaziada (HRESULT 0x{hResult:X8}).");
                    else
                        bytesFreed += Math.Max(0, recycleBinBytes - after.TotalBytes);
                }
                catch (Exception ex)
                {
                    errors.Add($"Esvaziamento da lixeira: {ex.Message}");
                }
            }

            return new CleanupExecutionSummary(processed, deleted, skipped, bytesFreed, errors);
        }, cancellationToken);

        await RecordAsync(filesToClean, emptyRecycleBin, summary, startedAt);
        return summary;
    }

    /// <summary>
    /// Registra a limpeza no histórico para ela aparecer em Backups. Não há valor anterior para restaurar:
    /// os arquivos são apagados de vez, e o registro diz isso. Falha ao gravar o histórico não desfaz nada.
    /// </summary>
    private async Task RecordAsync(IReadOnlyList<CleanupFileItem> files, bool emptyRecycleBin, CleanupExecutionSummary summary, DateTimeOffset startedAt)
    {
        if (_history is null)
            return;
        var names = GetSupportedCategories()
            .Where(info => files.Any(file => file.Category == info.Type) || (emptyRecycleBin && info.Type == CleanupCategoryType.RecycleBin))
            .Select(info => info.Title)
            .ToArray();
        var message = $"{summary.TotalDeleted:N0} arquivo(s) apagado(s), {summary.TotalSkipped:N0} ignorado(s) (em uso, protegidos ou fora da pasta), " +
                      $"{CleanupFileItem.FormatBytes(summary.TotalBytesFreed)} liberados." +
                      (summary.Errors.Count == 0 ? "" : $" Erros: {string.Join("; ", summary.Errors.Take(3))}");
        try
        {
            await _history.AppendAsync(new TweakOperationResult(Guid.NewGuid(), OperationPrefix + "run", startedAt,
                $"{files.Count(file => file.Category != CleanupCategoryType.RecycleBin):N0} arquivo(s) selecionado(s)",
                "Limpeza: " + (names.Length == 0 ? "nenhuma categoria" : string.Join(", ", names)),
                summary.Errors.Count == 0 ? "Sucesso verificado" : "Concluído com erros",
                message, false, null, "Sem desfazer: os arquivos foram apagados definitivamente."));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            Diagnostics.AppLog.Warn("Limpeza concluída, mas o histórico não foi gravado", ex);
        }
    }

    private bool IsInsideCategoryFolder(CleanupFileItem item)
    {
        var folder = _folders(item.Category);
        if (string.IsNullOrWhiteSpace(folder))
            return false;
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(item.FullPath).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetFolderPath(CleanupCategoryType category) => category switch
    {
        CleanupCategoryType.UserTemp => Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath(),
        CleanupCategoryType.SystemTemp => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
        CleanupCategoryType.WindowsUpdateCache => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download"),
        CleanupCategoryType.SystemLogs => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps"),
        CleanupCategoryType.ShaderCache => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache"),
        _ => null
    };
}
