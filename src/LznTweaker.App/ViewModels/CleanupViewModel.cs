using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using LznTweaker.Core.Cleanup;
using LznTweaker.Infrastructure;

namespace LznTweaker.ViewModels;

public sealed class CleanupCategoryItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isScanning;
    private string _statusText = "Aguardando verificação…";
    private CleanupScanResult? _scanResult;

    public CleanupCategoryItemViewModel(CleanupCategoryInfo info)
    {
        Info = info;
        // A Lixeira guarda arquivos pessoais excluídos; só é esvaziada se o usuário marcá-la.
        _isSelected = info.Type != CleanupCategoryType.RecycleBin;
    }

    public CleanupCategoryInfo Info { get; }
    public CleanupCategoryType CategoryType => Info.Type;
    public string Title => Info.Title;
    public string Description => Info.Description;
    public string PathSummary => Info.PathSummary;
    public bool RequiresElevation => Info.RequiresElevation;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (_isScanning == value) return;
            _isScanning = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText == value) return;
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public CleanupScanResult? ScanResult
    {
        get => _scanResult;
        set
        {
            _scanResult = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TotalBytes));
            OnPropertyChanged(nameof(SizeDisplay));
        }
    }

    public long TotalBytes => ScanResult?.TotalBytes ?? 0;
    public string SizeDisplay => ScanResult is null ? "0 B" : ScanResult.TotalSizeText;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
}

public sealed class CleanupViewModel : INotifyPropertyChanged
{
    private readonly SystemCleanupService _cleanupService;
    private bool _isBusy;
    private string _statusMessage = "Pronto para verificar arquivos temporários e caches.";
    private double _progressValue;
    private string _progressText = "";
    private long _totalFoundBytes;
    private int _totalFoundFiles;

    private readonly IUserDialogs _dialogs;

    public CleanupViewModel(SystemCleanupService cleanupService, IUserDialogs dialogs)
    {
        _cleanupService = cleanupService;
        _dialogs = dialogs;
        ScanCommand = new AsyncRelayCommand(() => ScanAllAsync());
        CleanCommand = new AsyncRelayCommand(CleanWithConfirmationAsync);
        SelectAllCommand = new RelayCommand(() => SelectAll(true));
        SelectNoneCommand = new RelayCommand(() => SelectAll(false));
        Categories = new ObservableCollection<CleanupCategoryItemViewModel>(
            SystemCleanupService.GetSupportedCategories().Select(c => new CleanupCategoryItemViewModel(c)));

        foreach (var cat in Categories)
            cat.PropertyChanged += Category_PropertyChanged;
    }

    public ObservableCollection<CleanupCategoryItemViewModel> Categories { get; }
    public IAsyncRelayCommand ScanCommand { get; }
    public IAsyncRelayCommand CleanCommand { get; }
    public IRelayCommand SelectAllCommand { get; }
    public IRelayCommand SelectNoneCommand { get; }

    /// <summary>Chamado quando a página aparece: varre só na primeira vez (somente leitura).</summary>
    public async Task OnShownAsync()
    {
        if (Categories.All(category => category.ScanResult is null))
            await ScanAllAsync();
    }

    private async Task CleanWithConfirmationAsync()
    {
        if (!CanClean)
            return;
        if (_dialogs.Confirm(
                $"Deseja prosseguir com a remoção de {SelectedSizeText}?\n\n" +
                "• Arquivos temporários e caches serão apagados definitivamente.\n" +
                "• Arquivos em uso por programas abertos são ignorados e mantidos.\n" +
                "• A Lixeira será esvaziada, se estiver selecionada.\n\n" +
                "Deseja continuar?",
                "Confirmar limpeza"))
            await CleanSelectedAsync();
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanClean));
            OnPropertyChanged(nameof(CanScan));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value) return;
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    private ProblemInfo? _problem;
    /// <summary>Falha da varredura ou da limpeza em linguagem simples, mostrada no Banner (nulo quando deu certo).</summary>
    public ProblemInfo? Problem
    {
        get => _problem;
        private set
        {
            if (_problem == value) return;
            _problem = value;
            OnPropertyChanged();
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set
        {
            if (Math.Abs(_progressValue - value) < 0.1) return;
            _progressValue = value;
            OnPropertyChanged();
        }
    }

    public string ProgressText
    {
        get => _progressText;
        private set
        {
            if (_progressText == value) return;
            _progressText = value;
            OnPropertyChanged();
        }
    }

    public string TotalFoundText => CleanupFileItem.FormatBytes(_totalFoundBytes);
    public string TotalFilesText => $"{_totalFoundFiles:N0} arquivos encontrados";

    public long SelectedBytes => Categories
        .Where(c => c.IsSelected && c.ScanResult is not null)
        .Sum(c => c.ScanResult!.TotalBytes);

    public string SelectedSizeText => CleanupFileItem.FormatBytes(SelectedBytes);

    public int SelectedFilesCount => Categories
        .Where(c => c.IsSelected && c.ScanResult is not null)
        .Sum(c => c.ScanResult!.FileCount);

    public bool CanScan => !IsBusy;
    public bool CanClean => !IsBusy && Categories.Any(c => c.IsSelected && c.TotalBytes > 0);

    public async Task ScanAllAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy) return;
        IsBusy = true;
        Problem = null;
        StatusMessage = "Varrendo diretórios de temporários, logs e caches do Windows…";
        ProgressValue = 0;
        _totalFoundBytes = 0;
        _totalFoundFiles = 0;

        try
        {
            int index = 0;
            foreach (var category in Categories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                category.IsScanning = true;
                category.StatusText = "Analisando arquivos…";

                var result = await _cleanupService.ScanCategoryAsync(category.CategoryType, cancellationToken);
                category.ScanResult = result;
                category.IsScanning = false;
                category.StatusText = $"{Plural.Of(result.FileCount, "arquivo", "arquivos")} · {result.TotalSizeText}";
                // Área sem nada para apagar não fica marcada: o total marcado mostra só o que a limpeza vai liberar.
                if (result.TotalBytes == 0)
                    category.IsSelected = false;

                _totalFoundBytes += result.TotalBytes;
                _totalFoundFiles += result.FileCount;

                index++;
                ProgressValue = (index * 100.0) / Categories.Count;
            }

            // O total já aparece no topo e na barra de ação; aqui fica só a contagem de arquivos.
            StatusMessage = $"Varredura concluída: {TotalFilesText}.";
            UpdateCounters();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Varredura cancelada pelo usuário.";
        }
        catch (Exception ex)
        {
            Problem = ProblemInfo.FromException(ex, "A varredura não foi concluída");
            StatusMessage = "Varredura interrompida.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<CleanupExecutionSummary?> CleanSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (!CanClean || IsBusy) return null;
        IsBusy = true;
        ProgressValue = 0;

        var selectedCategories = Categories.Where(c => c.IsSelected && c.ScanResult is not null).ToArray();
        var filesToClean = selectedCategories
            .SelectMany(c => c.ScanResult!.Files)
            .ToArray();
        bool cleanRecycleBin = selectedCategories.Any(c => c.CategoryType == CleanupCategoryType.RecycleBin);

        StatusMessage = $"Iniciando limpeza segura de {filesToClean.Length:N0} itens…";

        CleanupExecutionSummary summary;
        try
        {
            var progress = new Progress<CleanupExecutionProgress>(p =>
            {
                ProgressValue = p.TotalFiles > 0 ? (p.ProcessedFiles * 100.0) / p.TotalFiles : 100;
                ProgressText = $"{p.ProcessedFiles}/{p.TotalFiles} · Liberados: {CleanupFileItem.FormatBytes(p.BytesFreed)}";
                StatusMessage = $"Removendo: {p.CurrentItemName}";
            });

            summary = await _cleanupService.CleanFilesAsync(filesToClean, cleanRecycleBin, progress, cancellationToken);
        }
        catch (Exception ex)
        {
            Problem = ProblemInfo.FromException(ex, "A limpeza não foi concluída");
            StatusMessage = "Limpeza interrompida. Rode a varredura de novo para ver o que restou.";
            return null;
        }
        finally
        {
            IsBusy = false;
        }

        var resultMessage = $"Limpeza concluída: {CleanupFileItem.FormatBytes(summary.TotalBytesFreed)} liberados ({Plural.Of(summary.TotalDeleted, "arquivo removido", "arquivos removidos")}; {Plural.Of(summary.TotalSkipped, "ignorado", "ignorados")} por estarem em uso, sem permissão ou ausentes).";
        if (summary.Errors.Count > 0)
            resultMessage += $" Avisos: {string.Join(" | ", summary.Errors)}";

        // Reanalisa para que os tamanhos exibidos correspondam ao que restou no disco.
        await ScanAllAsync(cancellationToken);
        StatusMessage = resultMessage;
        return summary;
    }

    public void SelectAll(bool select)
    {
        foreach (var cat in Categories)
            cat.IsSelected = select;
        UpdateCounters();
    }

    private void Category_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CleanupCategoryItemViewModel.IsSelected))
        {
            UpdateCounters();
        }
    }

    private void UpdateCounters()
    {
        OnPropertyChanged(nameof(SelectedBytes));
        OnPropertyChanged(nameof(SelectedSizeText));
        OnPropertyChanged(nameof(SelectedFilesCount));
        OnPropertyChanged(nameof(TotalFoundText));
        OnPropertyChanged(nameof(TotalFilesText));
        OnPropertyChanged(nameof(CanClean));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
}
