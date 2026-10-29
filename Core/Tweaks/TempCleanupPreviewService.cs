using System.IO;

namespace LznTweaker.Core.Tweaks;

public sealed record TempCleanupFile(string FullPath, long Length, long LastWriteTimeUtcTicks)
{
    public string DisplayName => Path.GetFileName(FullPath);
    public string SizeText => Length >= 1_073_741_824
        ? $"{Length / 1_073_741_824d:0.00} GB"
        : Length >= 1_048_576
            ? $"{Length / 1_048_576d:0.0} MB"
            : $"{Length / 1_024d:0} KB";
}

/// <summary>Creates a read-only preview of regular files in the current user's TEMP tree.</summary>
public sealed class TempCleanupPreviewService
{
    public string RootPath => Path.GetFullPath(
        Environment.GetEnvironmentVariable("TEMP") is { Length: > 0 } temp ? temp : Path.GetTempPath());

    public Task<IReadOnlyList<TempCleanupFile>> ScanAsync(CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<TempCleanupFile>>(() => Scan(cancellationToken), cancellationToken);

    private IReadOnlyList<TempCleanupFile> Scan(CancellationToken cancellationToken)
    {
        var root = RootPath;
        if (!Directory.Exists(root))
            return [];

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false
        };
        var files = new List<TempCleanupFile>();
        try
        {
            foreach (var path in Directory.EnumerateFiles(root, "*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var attributes = File.GetAttributes(path);
                    if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                        continue;

                    var info = new FileInfo(path);
                    files.Add(new TempCleanupFile(Path.GetFullPath(path), info.Length, info.LastWriteTimeUtc.Ticks));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    // A file can disappear or become inaccessible while the preview is being collected.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Enumeration can still return the files already collected when part of TEMP is inaccessible.
        }

        return files.OrderBy(file => file.FullPath, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
