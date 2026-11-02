using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using LznTweaker.Core;

namespace LznTweaker.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private readonly SystemInformationService _systemInformationService;
    private bool _isBusy;
    private string _statusMessage = "Aguardando leitura do sistema…";
    private string _processorDetail = "";
    private string _memorySummary = "Lendo informações…";
    private string _memoryDetail = "";
    private string _storageSummary = "Lendo informações…";
    private string _graphicsSummary = "Lendo placa de vídeo…";
    private string _graphicsMemorySummary = "VRAM dedicada não disponível nesta consulta";
    private string _motherboardSummary = "Lendo placa-mãe…";
    private string _commitSummary = "Memória comprometida: não disponível";
    private string _diskActivitySummary = "Atividade do disco: não disponível";
    private double _cpuUsage;

    public DashboardViewModel(SystemInformationService systemInformationService) =>
        _systemInformationService = systemInformationService;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => Set(ref _statusMessage, value); }
    public string ProcessorDetail { get => _processorDetail; private set => Set(ref _processorDetail, value); }
    /// <summary>Só o nome do processador, sem espaços sobrando (linha de hardware do Início).</summary>
    public string ProcessorName { get => _processorName; private set => Set(ref _processorName, value); }
    private string _processorName = "Lendo processador…";
    public string MemorySummary { get => _memorySummary; private set => Set(ref _memorySummary, value); }
    public string MemoryDetail { get => _memoryDetail; private set => Set(ref _memoryDetail, value); }
    public string StorageSummary { get => _storageSummary; private set => Set(ref _storageSummary, value); }
    public string GraphicsSummary { get => _graphicsSummary; private set => Set(ref _graphicsSummary, value); }
    public string GraphicsMemorySummary { get => _graphicsMemorySummary; private set => Set(ref _graphicsMemorySummary, value); }
    public string MotherboardSummary { get => _motherboardSummary; private set => Set(ref _motherboardSummary, value); }
    public string CommitSummary { get => _commitSummary; private set => Set(ref _commitSummary, value); }
    public string DiskActivitySummary { get => _diskActivitySummary; private set => Set(ref _diskActivitySummary, value); }
    public double CpuUsage { get => _cpuUsage; private set => Set(ref _cpuUsage, value); }
    public string CpuUsageText => $"{CpuUsage:0}%";

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        StatusMessage = "Consultando informações do computador…";
        try
        {
            var snapshot = await _systemInformationService.ReadAsync(cancellationToken);
            UpdateFrom(snapshot);
            StatusMessage = snapshot.CollectionWarnings.Count == 0
                ? "Leitura concluída · somente consulta"
                : $"Leitura parcial · indisponível: {string.Join(", ", snapshot.CollectionWarnings)}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Leitura cancelada.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Não foi possível concluir a leitura: {GetFriendlyError(ex)}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateFrom(SystemSnapshot snapshot)
    {
        ProcessorName = string.Join(' ', snapshot.ProcessorName.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        ProcessorDetail = snapshot.ProcessorName +
            (snapshot.ProcessorThreads > 0 ? $" · {snapshot.ProcessorThreads} threads" : "") +
            (snapshot.ProcessorClockMHz > 0 ? $" · até {snapshot.ProcessorClockMHz:N0} MHz" : "");

        MemorySummary = snapshot.MemoryTotalBytes > 0 ? FormatMemory(snapshot.MemoryTotalBytes) : "Não identificada";
        var usedBytes = snapshot.MemoryAvailableBytes is long availableMemory
            ? Math.Max(0, snapshot.MemoryTotalBytes - availableMemory)
            : 0;
        MemoryDetail = snapshot.MemoryTotalBytes > 0
            ? snapshot.MemoryAvailableBytes is long available
                ? $"{FormatMemory(usedBytes)} em uso · {FormatMemory(available)} disponível" + (snapshot.MemorySpeedMHz > 0 ? $" · {snapshot.MemorySpeedMHz:N0} MHz" : "")
                : "RAM disponível: não disponível" + (snapshot.MemorySpeedMHz > 0 ? $" · {snapshot.MemorySpeedMHz:N0} MHz" : "")
            : "Memória instalada não informada";

        var systemDrive = snapshot.Drives.FirstOrDefault(drive =>
            string.Equals(drive.Name, Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        StorageSummary = systemDrive is null
            ? snapshot.Drives.Count == 0 ? "Nenhum volume disponível" : $"{snapshot.Drives.Count} volumes prontos"
            : $"{systemDrive.Name} · {systemDrive.TotalText} · {systemDrive.FreeText}";

        GraphicsSummary = string.IsNullOrWhiteSpace(snapshot.GraphicsAdapters)
            ? "Placa de vídeo não identificada"
            : snapshot.GraphicsAdapters;
        GraphicsMemorySummary = snapshot.PrimaryGraphics switch
        {
            null => "VRAM e driver não disponíveis nesta leitura.",
            var gpu => string.Join(" · ", new[]
            {
                gpu.DedicatedMemoryBytes is long vram ? $"{FormatMemory(vram)} de VRAM" : "VRAM não informada pelo driver",
                gpu.VendorSoftwareVersion is { } vendor ? $"driver {vendor}" : gpu.DriverVersion is { } driver ? $"driver {driver}" : null
            }.Where(part => part is not null))
        };
        MotherboardSummary = snapshot.Motherboard;
        CommitSummary = snapshot.MemoryCommitLimitBytes is > 0 && snapshot.MemoryCommittedBytes is long committed
            ? $"Comprometida: {FormatMemory(committed)} de {FormatMemory(snapshot.MemoryCommitLimitBytes.Value)}"
            : "Memória comprometida: não disponível";
        DiskActivitySummary = snapshot.DiskActivity is null
            ? "Atividade do disco: não disponível neste Windows"
            : $"Disco · leitura {FormatRate(snapshot.DiskActivity.ReadBytesPerSecond)} · gravação {FormatRate(snapshot.DiskActivity.WriteBytesPerSecond)} · ativo {snapshot.DiskActivity.BusyPercent:0}%";
        CpuUsage = snapshot.CpuUsagePercent;
        OnPropertyChanged(nameof(CpuUsageText));
    }

    private static string FormatMemory(long bytes) => $"{bytes / 1_073_741_824d:0.0} GB";
    private static string FormatRate(long bytesPerSecond) => bytesPerSecond >= 1_000_000
        ? $"{bytesPerSecond / 1_000_000d:0.0} MB/s"
        : $"{bytesPerSecond / 1_000d:0} KB/s";

    private static string GetFriendlyError(Exception exception)
    {
        var root = exception;
        while (root.InnerException is not null)
            root = root.InnerException;

        return root switch
        {
            UnauthorizedAccessException => "acesso negado pelo Windows",
            COMException => "o serviço de inventário do Windows não respondeu",
            _ => root.Message
        };
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
