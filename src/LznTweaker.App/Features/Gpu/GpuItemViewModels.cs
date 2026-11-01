using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LznTweaker.Core.Gpu;

using LznTweaker.Infrastructure;

namespace LznTweaker.Features.Gpu;

/// <summary>Estados exibidos na página. "Aplicado" e "Restaurado" só valem para mudanças feitas pelo próprio app.</summary>
public enum GpuItemState { Applied, ManualRequired, NotSupported, NotDetected, Failed, Restored }

public static class GpuStateText
{
    private static SolidColorBrush Green => ThemeBrushes.Success;
    private static SolidColorBrush Amber => ThemeBrushes.Warning;
    private static SolidColorBrush Red => ThemeBrushes.Danger;
    private static SolidColorBrush Gray => ThemeBrushes.Muted;

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

}

/// <summary>Uma configuração do driver (Painel de Controle NVIDIA ou AMD Software: Adrenalin Edition).</summary>
public sealed partial class DriverSettingViewModel : ObservableObject
{
    public DriverSettingViewModel(GpuDriverSetting setting) => Setting = setting;

    public GpuDriverSetting Setting { get; }
    public string RecommendedValue => Setting.RecommendedValue;
    public string Name => Setting.Name;
    public string ManualPath => Setting.ManualPath;
    public IReadOnlyList<string> Options => Setting.Options;
    public GpuSettingGuide Guide => Setting.Guide;
    public string? Caution => Setting.Caution;
    public string MethodText => GpuStateText.MethodText(Setting.Method);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsState))]
    private string _stateText = "";

    [ObservableProperty]
    private Brush _stateBrush = Brushes.Gray;

    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>"Requer configuração manual" vale para a aba inteira e aparece uma vez no topo, não em cada linha.</summary>
    public bool ShowsState => StateText != GpuStateText.For(GpuItemState.ManualRequired).Text;

    /// <summary>O app não lê o valor atual do driver; diz isso em vez de supor.</summary>
    public string CurrentValueText => "Não detectado: o LZN Tweaker não lê as configurações do driver";

    public void SetState(GpuItemState state)
    {
        (StateText, StateBrush) = GpuStateText.For(state);
    }
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
