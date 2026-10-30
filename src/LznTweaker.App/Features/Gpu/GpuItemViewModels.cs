using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LznTweaker.Core.Gpu;

namespace LznTweaker.Features.Gpu;

/// <summary>Estados exibidos na página. "Aplicado" e "Restaurado" só valem para mudanças feitas pelo próprio app.</summary>
public enum GpuItemState { Applied, ManualRequired, NotSupported, NotDetected, Failed, Restored }

public static class GpuStateText
{
    private static readonly SolidColorBrush Green = Freeze("#6EE7B7");
    private static readonly SolidColorBrush Amber = Freeze("#F5C46B");
    private static readonly SolidColorBrush Red = Freeze("#F87171");
    private static readonly SolidColorBrush Gray = Freeze("#8A8A96");

    public static (string Text, Brush Brush) For(GpuItemState state) => state switch
    {
        GpuItemState.Applied => ("Aplicado", Green),
        GpuItemState.ManualRequired => ("Requer configuração manual", Amber),
        GpuItemState.NotSupported => ("Não suportado nesta GPU", Gray),
        GpuItemState.NotDetected => ("Não detectado", Gray),
        GpuItemState.Failed => ("Falha ao aplicar", Red),
        _ => ("Restaurado", Green)
    };

    public static string VendorName(GpuVendor vendor) => vendor switch
    {
        GpuVendor.Nvidia => "NVIDIA GeForce",
        GpuVendor.Amd => "AMD Radeon",
        GpuVendor.Intel => "Intel",
        _ => "Não identificado"
    };

    public static string MethodText(GpuApplyMethod method) => method switch
    {
        GpuApplyMethod.Automatic => "Automática",
        GpuApplyMethod.PartiallyAutomatic => "Parcialmente automática",
        _ => "Manual"
    };

    private static SolidColorBrush Freeze(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}

public sealed partial class GpuModeOptionViewModel(GpuOptimizationMode mode, string title, string description, string glyph) : ObservableObject
{
    public GpuOptimizationMode Mode { get; } = mode;
    public string Title { get; } = title;
    public string Description { get; } = description;
    public string Glyph { get; } = glyph;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>Uma configuração do driver (Painel de Controle NVIDIA ou AMD Software: Adrenalin Edition).</summary>
public sealed partial class DriverSettingViewModel : ObservableObject
{
    private readonly Action<DriverSettingViewModel> _customChanged;

    public DriverSettingViewModel(GpuDriverSetting setting, Action<DriverSettingViewModel> customChanged)
    {
        Setting = setting;
        _customChanged = customChanged;
    }

    public GpuDriverSetting Setting { get; }
    public string Name => Setting.Name;
    public string ManualPath => Setting.ManualPath;
    public IReadOnlyList<string> Options => Setting.Options;
    public GpuSettingGuide Guide => Setting.Guide;
    public string? Caution => Setting.Caution;
    public string MethodText => GpuStateText.MethodText(Setting.Method);

    [ObservableProperty]
    private string _recommendedValue = "";

    [ObservableProperty]
    private bool _isCustomMode;

    [ObservableProperty]
    private string? _customValue;

    [ObservableProperty]
    private string _stateText = "";

    [ObservableProperty]
    private Brush _stateBrush = Brushes.Gray;

    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>O app não lê o valor atual do driver; diz isso em vez de supor.</summary>
    public string CurrentValueText => "Não detectado: o LZN Tweaker não lê as configurações do driver";

    public void SetState(GpuItemState state)
    {
        (StateText, StateBrush) = GpuStateText.For(state);
    }

    partial void OnCustomValueChanged(string? value) => _customChanged(this);
}

public sealed record GameSettingRow(string Name, string Value, string? Note, bool KeepsQualityForVisibility);

public sealed record VendorAdviceRow(string Vendor, string Recommendation, string Detail, bool IsDetectedVendor);

/// <summary>Perfil de um jogo: recomendações do driver e opções do menu do jogo, sem alterar arquivos do jogo.</summary>
public sealed partial class GpuGameViewModel : ObservableObject
{
    public GpuGameViewModel(GpuGameProfile profile, ImageSource? icon, bool? installed)
    {
        Profile = profile;
        Icon = icon;
        InstalledText = installed switch
        {
            true => "Instalado",
            false => "Não encontrado neste PC",
            _ => "Instalação não verificada"
        };
        RebuildSettings();
    }

    public GpuGameProfile Profile { get; }
    public string Name => Profile.Name;
    public string ShortName => Profile.ShortName;
    public string MenuPath => Profile.MenuPath;
    public string TestAdvice => Profile.TestAdvice;
    public string? Caveat => Profile.Caveat;
    public ImageSource? Icon { get; }
    public bool HasIcon => Icon is not null;
    public string InstalledText { get; }

    public IReadOnlyList<GameSettingRow> Settings { get; private set; } = [];
    public IReadOnlyList<VendorAdviceRow> VendorAdvice { get; private set; } = [];

    [ObservableProperty]
    private bool _keepVisibility;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _recommendedProfile = "";

    [ObservableProperty]
    private string _fpsCapAdvice = "";

    [ObservableProperty]
    private string _stateText = "";

    [ObservableProperty]
    private Brush _stateBrush = Brushes.Gray;

    public void SetVendor(GpuVendor detected)
    {
        VendorAdvice = Profile.VendorAdvice
            .OrderByDescending(advice => advice.Vendor == detected)
            .Select(advice => new VendorAdviceRow(GpuStateText.VendorName(advice.Vendor), advice.Recommendation, advice.Detail, advice.Vendor == detected))
            .ToArray();
        OnPropertyChanged(nameof(VendorAdvice));
    }

    public void SetState(GpuItemState state) => (StateText, StateBrush) = GpuStateText.For(state);

    partial void OnKeepVisibilityChanged(bool value) => RebuildSettings();

    private void RebuildSettings()
    {
        Settings = Profile.Settings
            .Select(setting => KeepVisibility && setting.VisibilityValue is { } visibility
                ? new GameSettingRow(setting.Name, visibility, setting.VisibilityNote ?? setting.Note, true)
                : new GameSettingRow(setting.Name, setting.LowValue, setting.Note, false))
            .ToArray();
        OnPropertyChanged(nameof(Settings));
    }
}
