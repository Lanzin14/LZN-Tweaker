namespace LznTweaker.Core;

public sealed record DriveSnapshot(string Name, long TotalBytes, long FreeBytes)
{
    public string TotalText => FormatBytes(TotalBytes);
    public string FreeText => $"{FormatBytes(FreeBytes)} livres";
    public double UsedPercent => TotalBytes <= 0 ? 0 : Math.Clamp((TotalBytes - FreeBytes) * 100d / TotalBytes, 0, 100);

    private static string FormatBytes(long bytes) => bytes >= 1_000_000_000_000
        ? $"{bytes / 1_000_000_000_000d:0.0} TB"
        : $"{bytes / 1_000_000_000d:0} GB";
}

public sealed record ProcessUsageSnapshot(string Name, long WorkingSetBytes)
{
    public string MemoryText => $"{WorkingSetBytes / 1_073_741_824d:0.00} GB";
}

public sealed record DiskActivitySnapshot(double BusyPercent, long ReadBytesPerSecond, long WriteBytesPerSecond);

public sealed record SystemSnapshot(
    string WindowsName,
    string WindowsVersion,
    string WindowsBuild,
    string Architecture,
    string ProcessorName,
    int ProcessorCores,
    int ProcessorThreads,
    int ProcessorClockMHz,
    long MemoryTotalBytes,
    int MemorySpeedMHz,
    long? MemoryAvailableBytes,
    long? MemoryCommittedBytes,
    long? MemoryCommitLimitBytes,
    string GraphicsAdapters,
    string Motherboard,
    TimeSpan Uptime,
    double CpuUsagePercent,
    DiskActivitySnapshot? DiskActivity,
    IReadOnlyList<DriveSnapshot> Drives,
    IReadOnlyList<ProcessUsageSnapshot> TopProcesses,
    IReadOnlyList<string> CollectionWarnings);

public sealed record SystemRecommendation(
    string Titulo,
    string DescricaoDetalhada,
    string NivelDeImpacto,
    bool SeguroParaAplicarAutomaticamente,
    string Tipo = "Informativo")
{
    public string AplicacaoAutomaticaText => SeguroParaAplicarAutomaticamente
        ? "Pode ser aplicada automaticamente"
        : "Não é aplicada automaticamente";
}
