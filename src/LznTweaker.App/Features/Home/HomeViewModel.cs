using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LznTweaker.Core.Tweaks;
using LznTweaker.Infrastructure;
using LznTweaker.Shell;
using LznTweaker.ViewModels;

namespace LznTweaker.Features.Home;

public sealed record KeyTweakState(string Title, string State, Brush Brush);

public sealed record HomeShortcut(string Glyph, string Title, string Description, string PageKey);

/// <summary>Resumo do sistema (Dashboard existente), estado dos ajustes principais e atalhos.</summary>
public sealed partial class HomeViewModel : ObservableObject
{
    private static readonly string[] KeyTweakIds =
    [
        TweakCatalog.EnableGameModeId, TweakCatalog.DisableGameDvrId, TweakCatalog.DisableTelemetryId,
        TweakCatalog.DisableFullscreenOptId, TweakCatalog.DisableBackgroundAppsId, TweakCatalog.DisableRecallId
    ];

    private static readonly SolidColorBrush Active = Freeze("#A78BFA");
    private static readonly SolidColorBrush Inactive = Freeze("#74747F");
    private static readonly SolidColorBrush Partial = Freeze("#F5C46B");

    private readonly INavigationService _navigation;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(60) };
    private bool _loadedOnce;

    public HomeViewModel(DashboardViewModel dashboard, INavigationService navigation)
    {
        Dashboard = dashboard;
        _navigation = navigation;
        _timer.Tick += async (_, _) => await Dashboard.RefreshAsync();
    }

    public DashboardViewModel Dashboard { get; }
    public ObservableCollection<KeyTweakState> KeyTweaks { get; } = [];

    public IReadOnlyList<HomeShortcut> Shortcuts { get; } =
    [
        new("", "General", "Ajustes de desempenho, privacidade e energia", ShellPages.General),
        new("", "Jogos", "Otimização por jogo e diagnóstico", ShellPages.Games),
        new("", "Debloat", "Apps pré-instalados", ShellPages.Debloat),
        new("", "Limpeza", "Temporários e caches com prévia", ShellPages.Cleanup),
        new("", "Backups", "Histórico e restauração", ShellPages.Backups),
        new("", "Fixes", "SFC, DISM e reparos", ShellPages.Fixes)
    ];

    /// <summary>Chamado quando a página aparece: lê na primeira vez e atualiza a cada minuto enquanto visível.</summary>
    public async Task OnShownAsync()
    {
        _timer.Start();
        await RefreshKeyTweaksAsync();
        if (!_loadedOnce)
        {
            _loadedOnce = true;
            await Dashboard.RefreshAsync();
        }
    }

    public void OnHidden() => _timer.Stop();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await RefreshKeyTweaksAsync();
        await Dashboard.RefreshAsync();
    }

    [RelayCommand]
    private void Navigate(string? pageKey)
    {
        if (pageKey is not null)
            _navigation.Navigate(pageKey);
    }

    private async Task RefreshKeyTweaksAsync()
    {
        var states = await Task.Run(() => KeyTweakIds.Select(id =>
        {
            var definition = TweakCatalog.GetById(id);
            var (state, _) = TweakStateDetector.Detect(definition);
            return (definition.Name, state);
        }).ToArray());
        KeyTweaks.Clear();
        foreach (var (name, state) in states)
        {
            KeyTweaks.Add(state switch
            {
                TweakSystemState.Enabled => new KeyTweakState(name, "Ativo", Active),
                TweakSystemState.Disabled => new KeyTweakState(name, "Inativo", Inactive),
                TweakSystemState.Mixed => new KeyTweakState(name, "Parcial", Partial),
                TweakSystemState.Unsupported => new KeyTweakState(name, "Indisponível", Inactive),
                _ => new KeyTweakState(name, "Não verificado", Inactive)
            });
        }
    }

    private static SolidColorBrush Freeze(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
