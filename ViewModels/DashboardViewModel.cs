using System.Collections.ObjectModel;
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
    private string _lastUpdated = "Ainda não atualizado";
    private string _operatingSystemName = "Lendo informações…";
    private string _operatingSystemDetail = "";
    private string _processorSummary = "Lendo informações…";
    private string _processorDetail = "";
    private string _memorySummary = "Lendo informações…";
    private string _memoryDetail = "";
    private string _storageSummary = "Lendo informações…";
    private string _storageDetail = "";
    private string _graphicsSummary = "Lendo placa de vídeo…";
    private string _graphicsMemorySummary = "VRAM dedicada não disponível nesta consulta";
    private string _motherboardSummary = "Lendo placa-mãe…";
    private string _uptimeSummary = "Calculando tempo desde a inicialização…";
    private string _commitSummary = "Memória comprometida: não disponível";
    private string _diskActivitySummary = "Atividade do disco: não disponível";
    private double _cpuUsage;
    private double _memoryUsage;
    private double _systemDriveUsage;

    public DashboardViewModel(SystemInformationService systemInformationService) =>
        _systemInformationService = systemInformationService;

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<SystemRecommendation> Recommendations { get; } = [];
    public ObservableCollection<DriveSnapshot> Drives { get; } = [];
    public ObservableCollection<ProcessUsageSnapshot> TopProcesses { get; } = [];

    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => Set(ref _statusMessage, value); }
    public string LastUpdated { get => _lastUpdated; private set => Set(ref _lastUpdated, value); }
    public string OperatingSystemName { get => _operatingSystemName; private set => Set(ref _operatingSystemName, value); }
    public string OperatingSystemDetail { get => _operatingSystemDetail; private set => Set(ref _operatingSystemDetail, value); }
    public string ProcessorSummary { get => _processorSummary; private set => Set(ref _processorSummary, value); }
    public string ProcessorDetail { get => _processorDetail; private set => Set(ref _processorDetail, value); }
    public string MemorySummary { get => _memorySummary; private set => Set(ref _memorySummary, value); }
    public string MemoryDetail { get => _memoryDetail; private set => Set(ref _memoryDetail, value); }
    public string StorageSummary { get => _storageSummary; private set => Set(ref _storageSummary, value); }
    public string StorageDetail { get => _storageDetail; private set => Set(ref _storageDetail, value); }
    public string GraphicsSummary { get => _graphicsSummary; private set => Set(ref _graphicsSummary, value); }
    public string GraphicsMemorySummary { get => _graphicsMemorySummary; private set => Set(ref _graphicsMemorySummary, value); }
    public string MotherboardSummary { get => _motherboardSummary; private set => Set(ref _motherboardSummary, value); }
    public string UptimeSummary { get => _uptimeSummary; private set => Set(ref _uptimeSummary, value); }
    public string CommitSummary { get => _commitSummary; private set => Set(ref _commitSummary, value); }
    public string DiskActivitySummary { get => _diskActivitySummary; private set => Set(ref _diskActivitySummary, value); }
    public double CpuUsage { get => _cpuUsage; private set => Set(ref _cpuUsage, value); }
    public string CpuUsageText => $"{CpuUsage:0}%";
    public double MemoryUsage { get => _memoryUsage; private set => Set(ref _memoryUsage, value); }
    public string MemoryUsageText => $"{MemoryUsage:0}%";
    public double SystemDriveUsage { get => _systemDriveUsage; private set => Set(ref _systemDriveUsage, value); }
    public string SystemDriveUsageText => $"{SystemDriveUsage:0}%";

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
            Recommendations.Clear();
            foreach (var recommendation in SystemRecommendationEngine.Analyze(snapshot))
                Recommendations.Add(recommendation);

            LastUpdated = $"Atualizado às {DateTime.Now:HH:mm:ss}";
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
            Recommendations.Clear();
            Recommendations.Add(new SystemRecommendation(
                "Diagnóstico parcial",
                "Não foi possível obter parte das informações do sistema. Nenhuma configuração foi alterada. Tente atualizar novamente.",
                "Baixo", false));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateFrom(SystemSnapshot snapshot)
    {
        OperatingSystemName = snapshot.WindowsName;
        OperatingSystemDetail = $"{snapshot.WindowsVersion} · {snapshot.WindowsBuild} · {snapshot.Architecture}";

        ProcessorSummary = snapshot.ProcessorCores > 0
            ? $"{snapshot.ProcessorCores} núcleos"
            : "Núcleos não informados";
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
        MemoryUsage = snapshot.MemoryTotalBytes > 0 && snapshot.MemoryAvailableBytes.HasValue
            ? Math.Clamp(usedBytes * 100d / snapshot.MemoryTotalBytes, 0, 100)
            : 0;

        var systemDrive = snapshot.Drives.FirstOrDefault(drive =>
            string.Equals(drive.Name, Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        if (systemDrive is null)
        {
            StorageSummary = snapshot.Drives.Count == 0 ? "Nenhum volume disponível" : $"{snapshot.Drives.Count} volumes prontos";
            StorageDetail = string.Join(" · ", snapshot.Drives.Select(drive => $"{drive.Name} {drive.FreeText}"));
            SystemDriveUsage = 0;
        }
        else
        {
            StorageSummary = $"{systemDrive.Name} · {systemDrive.TotalText}";
            StorageDetail = systemDrive.FreeText;
            SystemDriveUsage = systemDrive.UsedPercent;
        }

        GraphicsSummary = string.IsNullOrWhiteSpace(snapshot.GraphicsAdapters)
            ? "Placa de vídeo não identificada"
            : snapshot.GraphicsAdapters;
        GraphicsMemorySummary = "VRAM dedicada não disponível nesta consulta; memória compartilhada não foi tratada como VRAM.";
        MotherboardSummary = snapshot.Motherboard;
        UptimeSummary = $"Ligado há {FormatDuration(snapshot.Uptime)}";
        CommitSummary = snapshot.MemoryCommitLimitBytes is > 0 && snapshot.MemoryCommittedBytes is long committed
            ? $"Comprometida: {FormatMemory(committed)} de {FormatMemory(snapshot.MemoryCommitLimitBytes.Value)}"
            : "Memória comprometida: não disponível";
        DiskActivitySummary = snapshot.DiskActivity is null
            ? "Atividade do disco: não disponível neste Windows"
            : $"Disco · leitura {FormatRate(snapshot.DiskActivity.ReadBytesPerSecond)} · gravação {FormatRate(snapshot.DiskActivity.WriteBytesPerSecond)} · ativo {snapshot.DiskActivity.BusyPercent:0}%";
        CpuUsage = snapshot.CpuUsagePercent;
        Drives.Clear();
        foreach (var drive in snapshot.Drives)
            Drives.Add(drive);
        TopProcesses.Clear();
        foreach (var process in snapshot.TopProcesses)
            TopProcesses.Add(process);
        OnPropertyChanged(nameof(CpuUsageText));
        OnPropertyChanged(nameof(MemoryUsageText));
        OnPropertyChanged(nameof(SystemDriveUsageText));
    }

    private static string FormatMemory(long bytes) => $"{bytes / 1_073_741_824d:0.0} GB";
    private static string FormatRate(long bytesPerSecond) => bytesPerSecond >= 1_000_000
        ? $"{bytesPerSecond / 1_000_000d:0.0} MB/s"
        : $"{bytesPerSecond / 1_000d:0} KB/s";
    private static string FormatDuration(TimeSpan duration) => duration.TotalDays >= 1
        ? $"{(int)duration.TotalDays}d {duration.Hours}h"
        : duration.TotalHours >= 1 ? $"{duration.Hours}h {duration.Minutes}min" : $"{duration.Minutes}min";

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
