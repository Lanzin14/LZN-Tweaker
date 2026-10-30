using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LznTweaker.Core.GameOptimization;
using LznTweaker.Core.Gpu;
using LznTweaker.Features.TweakCards;
using LznTweaker.Infrastructure;

namespace LznTweaker.Features.Gpu;

/// <summary>
/// Central de configuração de GPU. Detecta a placa e o monitor, recomenda valores por configuração e por jogo e guarda as
/// escolhas e resultados do usuário. Não altera driver, registro nem arquivos de jogo: cada ajuste traz o caminho
/// para ser feito manualmente, porque as interfaces documentadas (NVAPI e AMD ADLX) ainda não estão integradas.
/// </summary>
public sealed partial class GpuViewModel : ObservableObject
{
    private static SolidColorBrush Green => ThemeBrushes.Success;
    private static SolidColorBrush Gray => ThemeBrushes.Muted;
    private static SolidColorBrush Amber => ThemeBrushes.Warning;

    private readonly GpuDetectionService _detection;
    private readonly GpuPreferencesStore _store;
    private readonly GameLocator _games;
    private readonly INotificationService _notifications;
    private GpuPreferences _preferences;
    private GpuDetectionResult _result = GpuDetectionResult.NotChecked;
    private bool _initialized;

    public GpuViewModel(GpuDetectionService detection, GpuPreferencesStore store, GameLocator games, INotificationService notifications)
    {
        _detection = detection;
        _store = store;
        _games = games;
        _notifications = notifications;
        _preferences = store.Load();

        Tabs =
        [
            new("nvidia", "NVIDIA GeForce", ""),
            new("amd", "AMD Radeon", ""),
            new("games", "Perfis por jogo", "", "Opções do menu de cada jogo com prioridade para o LOW e o que ajustar no driver para cada um."),
            new("monitor", "Monitor e Latência", "", "Como combinar V-Sync, G-SYNC/FreeSync, Reflex ou Anti-Lag e limite de FPS conforme o seu monitor.")
        ];
        foreach (var tab in Tabs)
            tab.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(CardTabViewModel.IsSelected) && sender is CardTabViewModel { IsSelected: true } selected)
                {
                    SelectedTabKey = selected.Key;
                    SelectedTabDescription = selected.Description;
                }
            };

        NvidiaSettings = new(GpuCatalog.DriverSettings.Where(s => s.Vendor == GpuVendor.Nvidia).Select(s => new DriverSettingViewModel(s)));
        AmdSettings = new(GpuCatalog.DriverSettings.Where(s => s.Vendor == GpuVendor.Amd).Select(s => new DriverSettingViewModel(s)));
        Scenarios = GpuCatalog.Scenarios;
        Tabs[0].IsSelected = true;
    }

    public IReadOnlyList<CardTabViewModel> Tabs { get; }
    public ObservableCollection<DriverSettingViewModel> NvidiaSettings { get; }
    public ObservableCollection<DriverSettingViewModel> AmdSettings { get; }
    public ObservableCollection<GpuGameViewModel> Games { get; } = [];
    public IReadOnlyList<GpuLatencyScenario> Scenarios { get; }
    public string AutomationNote => GpuCatalog.AutomationNote;

    // ─── Visão geral ───
    [ObservableProperty] private string _vendorText = "Não verificado";
    [ObservableProperty] private string _modelText = "Não verificado";
    [ObservableProperty] private string _driverPackageText = "Não verificado";
    [ObservableProperty] private string _driverVersionText = "Não verificado";
    [ObservableProperty] private string _driverDateText = "Não verificado";
    [ObservableProperty] private string _memoryText = "Não verificado";
    /// <summary>Painel "Saiba mais" da identificação da placa; começa recolhido.</summary>
    [ObservableProperty] private bool _isOverviewExpanded;
    [ObservableProperty] private string _detectionText = "Não verificado";
    [ObservableProperty] private Brush _detectionBrush = Gray;
    [ObservableProperty] private string _detectionDetail = "";
    [ObservableProperty] private bool _isDetecting;
    [ObservableProperty] private string _selectedTabKey = "nvidia";
    [ObservableProperty] private string _selectedTabDescription = "";
    [ObservableProperty] private string _nvidiaBanner = "";
    [ObservableProperty] private string _amdBanner = "";
    [ObservableProperty] private GpuGameViewModel? _selectedGame;

    // ─── Monitor e Latência ───
    [ObservableProperty] private string _refreshRateText = "Não verificado";
    [ObservableProperty] private string _capAdvice = "";
    [ObservableProperty] private bool _vrrAvailable;
    [ObservableProperty] private bool _vrrNotAvailable;
    [ObservableProperty] private bool _vrrNotAnswered = true;

    public bool IsNvidiaTab => SelectedTabKey == "nvidia";
    public bool IsAmdTab => SelectedTabKey == "amd";
    public bool IsGamesTab => SelectedTabKey == "games";
    public bool IsMonitorTab => SelectedTabKey == "monitor";

    partial void OnSelectedTabKeyChanged(string value)
    {
        foreach (var name in new[] { nameof(IsNvidiaTab), nameof(IsAmdTab), nameof(IsGamesTab), nameof(IsMonitorTab) })
            OnPropertyChanged(name);
    }

    /// <summary>A página só lê o sistema quando aparece pela primeira vez; nada é aplicado ao abrir.</summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
            return;
        _initialized = true;
        await DetectAsync();
    }

    [RelayCommand]
    private async Task DetectAsync()
    {
        if (IsDetecting)
            return;
        IsDetecting = true;
        try
        {
            _result = await Task.Run(_detection.Detect);
            var games = await Task.Run(() => GpuCatalog.Games.Select(profile =>
            {
                var supported = SupportedGames.All.FirstOrDefault(game => game.Id == profile.GameId);
                bool? installed = supported is null ? null : _games.Locate(supported).IsInstalled;
                return (profile, installed);
            }).ToArray());

            var selectedId = SelectedGame?.Profile.GameId ?? GpuCatalog.Games[0].GameId;
            Games.Clear();
            foreach (var (profile, installed) in games)
                Games.Add(new GpuGameViewModel(profile, LocalArtwork.SteamAppIcon(_games.Steam.SteamRoot, profile.SteamAppId), installed));
            SelectedGame = Games.FirstOrDefault(game => game.Profile.GameId == selectedId) ?? Games.FirstOrDefault();
        }
        finally
        {
            IsDetecting = false;
        }

        UpdateOverview();
        RefreshStates();
        UpdateMonitor();
    }

    [RelayCommand]
    private void ToggleOverview() => IsOverviewExpanded = !IsOverviewExpanded;

    [RelayCommand]
    private void SelectGame(GpuGameViewModel? game)
    {
        if (game is not null)
            SelectedGame = game;
    }

    partial void OnSelectedGameChanged(GpuGameViewModel? value)
    {
        foreach (var game in Games)
            game.IsSelected = game == value;
    }

    [RelayCommand]
    private void ToggleDetails(DriverSettingViewModel? setting)
    {
        if (setting is not null)
            setting.IsExpanded = !setting.IsExpanded;
    }

    [RelayCommand]
    private void SetVrr(string? answer)
    {
        if (!Enum.TryParse<VrrAnswer>(answer, out var value))
            return;
        _preferences = _preferences with { Vrr = value };
        Save();
        UpdateMonitor();
    }

    private void UpdateOverview()
    {
        var primary = _result.Primary;
        switch (_result.Status)
        {
            case GpuDetectionStatus.Available when primary is not null:
                VendorText = GpuStateText.VendorName(primary.Vendor);
                ModelText = primary.Name;
                DriverPackageText = primary.VendorSoftwareVersion is { } vendorVersion
                    ? $"{(primary.Vendor == GpuVendor.Amd ? "AMD Software: Adrenalin Edition" : "NVIDIA GeForce")} {vendorVersion}"
                    : "Não identificado (só a versão do Windows está disponível)";
                DriverVersionText = primary.DriverVersion is { } version ? $"{version} (numeração do Windows)" : "Indisponível";
                DriverDateText = primary.DriverDate is { } date ? $"{date:dd/MM/yyyy}" : "Indisponível";
                MemoryText = primary.DedicatedMemoryBytes is { } bytes and > 0 ? $"{bytes / (1024.0 * 1024 * 1024):0.#} GB" : "Indisponível";
                DetectionText = "Disponível";
                DetectionBrush = Green;
                DetectionDetail = _result.Adapters.Count > 1
                    ? "Outras placas: " + string.Join(", ", _result.Adapters.Where(adapter => adapter != primary).Select(adapter => adapter.Name))
                    : "";
                break;
            default:
                VendorText = ModelText = DriverPackageText = DriverVersionText = DriverDateText = MemoryText = "Indisponível";
                DetectionText = _result.Status == GpuDetectionStatus.NotChecked ? "Não verificado" : "Indisponível";
                DetectionBrush = Amber;
                DetectionDetail = _result.Error ?? "";
                break;
        }

        var vendor = primary?.Vendor ?? GpuVendor.Unknown;
        NvidiaBanner = Banner(GpuVendor.Nvidia, vendor);
        AmdBanner = Banner(GpuVendor.Amd, vendor);
        foreach (var game in Games)
            game.SetVendor(vendor);

        // Abre a aba do fabricante detectado; a do outro continua disponível para consulta.
        var tabKey = vendor == GpuVendor.Amd ? "amd" : "nvidia";
        if (SelectedTabKey is "nvidia" or "amd")
            foreach (var tab in Tabs)
                tab.IsSelected = tab.Key == tabKey;
    }

    private string Banner(GpuVendor tabVendor, GpuVendor detected) => detected switch
    {
        _ when detected == tabVendor => $"Sua placa é {GpuStateText.VendorName(detected)}. Estes são os ajustes do driver dela, com o caminho para configurar cada um.",
        GpuVendor.Unknown => "A placa não foi identificada. As recomendações estão disponíveis só para consulta.",
        _ => $"Sua placa é {GpuStateText.VendorName(detected)}. Estas recomendações são apenas para consulta e não se aplicam à GPU instalada."
    };

    private void RefreshStates()
    {
        var vendor = _result.Primary?.Vendor ?? GpuVendor.Unknown;
        var detected = _result.Status == GpuDetectionStatus.Available;
        foreach (var item in NvidiaSettings.Concat(AmdSettings))
        {
            item.SetState(!detected ? GpuItemState.NotDetected
                : item.Setting.Vendor != vendor ? GpuItemState.NotSupported
                : GpuItemState.ManualRequired);
        }
        foreach (var game in Games)
            game.SetState(GpuItemState.ManualRequired);

    }

    private void UpdateMonitor()
    {
        var hz = _result.RefreshRateHz;
        RefreshRateText = hz is { } rate ? $"{rate} Hz (monitor principal, lido do Windows)" : "Não detectado";
        VrrAvailable = _preferences.Vrr == VrrAnswer.Available;
        VrrNotAvailable = _preferences.Vrr == VrrAnswer.NotAvailable;
        VrrNotAnswered = _preferences.Vrr == VrrAnswer.NotAnswered;
        CapAdvice = GpuCatalog.CapAdvice(_preferences.Vrr, hz);
        foreach (var game in Games)
            game.FpsCapAdvice = GpuCatalog.GameCapAdvice(game.Profile, _preferences.Vrr, hz);
    }

    private void Save()
    {
        try
        {
            _store.Save(_preferences);
        }
        catch (Exception ex)
        {
            _notifications.Notify("Não foi possível salvar as preferências", ex.Message, NotificationKind.Error);
        }
    }

}
