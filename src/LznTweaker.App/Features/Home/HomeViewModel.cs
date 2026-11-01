using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LznTweaker.Core.Diagnostics;
using LznTweaker.Core.GameOptimization;
using LznTweaker.Core.GameTuning;
using LznTweaker.Core.Tweaks;
using LznTweaker.Features.Backups;
using LznTweaker.Features.TweakCards;
using LznTweaker.Infrastructure;
using LznTweaker.Shell;
using LznTweaker.ViewModels;

namespace LznTweaker.Features.Home;

/// <summary>Última alteração registrada no Histórico.</summary>
public sealed record RecentChange(string Title, string Area, string When, string Outcome, bool Succeeded);

/// <summary>
/// Início como painel (docs/REESTRUTURACAO-UX.md, fase 4): o PC, três ações rápidas com números, as últimas
/// alterações e os jogos encontrados. Tudo é leitura; as ações só abrem a página certa (os ajustes recomendados ficam
/// marcados como pendentes, sem aplicar nada: a revisão e o ponto de restauração continuam na página Ajustes).
/// </summary>
public sealed partial class HomeViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly GeneralPageViewModel _tweaks;
    private readonly CleanupViewModel _cleanup;
    private readonly StartupViewModel _startup;
    private readonly TweakOperationHistory _history;
    private readonly GameLocator _games;
    private bool _initialized;

    public HomeViewModel(INavigationService navigation, DashboardViewModel dashboard, GeneralPageViewModel tweaks, CleanupViewModel cleanup,
        StartupViewModel startup, TweakOperationHistory history, GameLocator games)
    {
        _navigation = navigation;
        Dashboard = dashboard;
        _tweaks = tweaks;
        _cleanup = cleanup;
        _startup = startup;
        _history = history;
        _games = games;
        _tweaks.PropertyChanged += OnSourceChanged;
        _cleanup.PropertyChanged += OnSourceChanged;
        _startup.PropertyChanged += OnSourceChanged;
    }

    public DashboardViewModel Dashboard { get; }
    public string AppVersion => WindowsEnvironment.AppVersion;

    /// <summary>Ajustes da seleção recomendada que ainda não estão ativos.</summary>
    public int RecommendedMissing => _tweaks.Cards.OfType<TweakCardViewModel>()
        .Count(card => card.IsToggle && card.IsRecommended && !card.IsActive && card.IsSwitchEnabled);

    public string TweaksBadge => _tweaks.IsLoading ? "lendo…" : RecommendedMissing switch
    {
        0 => "em dia",
        1 => "1 pendente",
        var count => $"{count} pendentes"
    };
    public string TweaksText => RecommendedMissing == 0 && !_tweaks.IsLoading
        ? "A seleção recomendada já está ativa. Veja os demais ajustes por categoria."
        : "Marca a seleção recomendada para você revisar; nada muda sem confirmação e ponto de restauração.";

    // A varredura da Limpeza só roda ao abrir a página: no Início ela deixaria a abertura do app lenta.
    public string CleanupBadge => _cleanup.IsBusy ? "verificando…"
        : _cleanup.Categories.All(category => category.ScanResult is null) ? "não verificado" : _cleanup.TotalFoundText;
    public string StartupBadge => _startup.TotalCount == 0 ? "lendo…" : _startup.EnabledCount == 1 ? "1 ativado" : $"{_startup.EnabledCount} ativados";

    public IReadOnlyList<RecentChange> RecentChanges { get; private set; } = [];
    public bool HasRecentChanges => RecentChanges.Count > 0;
    public IReadOnlyList<string> InstalledGames { get; private set; } = [];
    public bool HasInstalledGames => InstalledGames.Count > 0;

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;
        _initialized = true;
        await Task.WhenAll(LoadRecentAsync(), LoadGamesAsync(),
            Dashboard.ProcessorDetail.Length == 0 ? Dashboard.RefreshAsync() : Task.CompletedTask);
        // Leituras mais lentas por último; cada cartão mostra "lendo…" até terminar.
        await _tweaks.InitializeAsync();
        await _startup.OnShownAsync();
    }

    [RelayCommand]
    private Task RefreshDashboardAsync() => Dashboard.RefreshAsync();

    [RelayCommand]
    private void ReviewRecommended()
    {
        _tweaks.ApplyPresetCommand.Execute(TweakPreset.Standard);
        _navigation.Navigate(ShellPages.General);
    }

    [RelayCommand]
    private void Navigate(string? pageKey)
    {
        if (pageKey is not null)
            _navigation.Navigate(pageKey);
    }

    private async Task LoadRecentAsync()
    {
        try
        {
            var entries = await _history.ReadAsync();
            RecentChanges = entries.OrderByDescending(entry => entry.StartedAt).Take(4)
                .Select(entry => new RecentChange(TitleOf(entry), BackupsViewModel.AreaOf(entry.TweakId), entry.StartedAt.LocalDateTime.ToString("dd/MM HH:mm"),
                    entry.Outcome, entry.Outcome is "Sucesso verificado" or "Reversão verificada" or "Executado sem verificação"))
                .ToArray();
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            AppLog.Warn("Início: histórico não lido", ex);
            RecentChanges = [];
        }
        OnPropertyChanged(nameof(RecentChanges));
        OnPropertyChanged(nameof(HasRecentChanges));
    }

    /// <summary>Reversões e perfis de jogo guardam só "Desfazer" ou o nome do perfil; aqui ganham o nome do ajuste ou do jogo.</summary>
    internal static string TitleOf(TweakOperationResult entry)
    {
        if (entry.RequestedState == "Desfazer")
        {
            var name = TweakCatalog.All.FirstOrDefault(definition => definition.Id == entry.TweakId)?.Name;
            return name is null ? "Desfazer" : $"Desfazer: {name}";
        }
        return entry.TweakId == GameTuningService.DeadlockOperationId ? $"Deadlock: perfil {entry.RequestedState}" : entry.RequestedState;
    }

    private async Task LoadGamesAsync()
    {
        InstalledGames = await Task.Run(() => SupportedGames.All.Select(_games.Locate).Where(game => game.IsInstalled)
            .Select(game => game.Game.Name).ToArray());
        OnPropertyChanged(nameof(InstalledGames));
        OnPropertyChanged(nameof(HasInstalledGames));
    }

    private void OnSourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(RecommendedMissing));
        OnPropertyChanged(nameof(TweaksBadge));
        OnPropertyChanged(nameof(TweaksText));
        OnPropertyChanged(nameof(CleanupBadge));
        OnPropertyChanged(nameof(StartupBadge));
    }
}
