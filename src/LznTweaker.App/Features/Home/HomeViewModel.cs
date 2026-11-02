using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LznTweaker.Core.Diagnostics;
using LznTweaker.Core.GameOptimization;
using LznTweaker.Core.Tweaks;
using LznTweaker.Features.Backups;
using LznTweaker.Features.TweakCards;
using LznTweaker.Infrastructure;
using LznTweaker.Shell;
using LznTweaker.ViewModels;

namespace LznTweaker.Features.Home;

/// <summary>Última alteração registrada no Histórico; registros seguidos do mesmo item viram uma linha só.</summary>
public sealed record RecentChange(string Title, string Detail, string When, string Outcome, string StatusText, bool Succeeded);

/// <summary>Jogo compatível encontrado neste PC, com o ícone local quando existe.</summary>
public sealed record HomeGame(string Name, string Platform, System.Windows.Media.ImageSource? Icon);

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

    public string TweaksBadge => _tweaks.IsLoading ? "Lendo…" : RecommendedMissing switch
    {
        0 => "Em dia",
        1 => "1 pendente",
        var count => $"{count} pendentes"
    };
    public string TweaksText => _tweaks.IsLoading
        ? "Lendo o estado dos ajustes do Windows."
        : RecommendedMissing == 0
            ? "A seleção recomendada já está ativa."
            : $"{Plural.Of(RecommendedMissing, "ajuste recomendado ainda não está ativo", "ajustes recomendados ainda não estão ativos")}.";

    // A varredura da Limpeza só roda ao abrir a página: no Início ela deixaria a abertura do app lenta.
    public string CleanupBadge => _cleanup.IsBusy ? "Verificando…"
        : IsCleanupUnscanned ? "Não verificado" : $"{_cleanup.TotalFoundText} para limpar";
    public bool IsCleanupUnscanned => !_cleanup.IsBusy && _cleanup.Categories.All(category => category.ScanResult is null);
    public bool IsTweaksLoading => _tweaks.IsLoading;
    public bool HasRecommendedPending => !_tweaks.IsLoading && RecommendedMissing > 0;
    public string StartupBadge => _startup.TotalCount == 0 ? "Lendo…" : Plural.Of(_startup.EnabledCount, "ativado", "ativados");

    public IReadOnlyList<RecentChange> RecentChanges { get; private set; } = [];
    public bool HasRecentChanges => RecentChanges.Count > 0;
    public IReadOnlyList<HomeGame> InstalledGames { get; private set; } = [];
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
            // Registros seguidos do mesmo item (ex.: o perfil do Deadlock aplicado duas vezes) viram uma linha com a contagem.
            var groups = new List<(TweakOperationResult Latest, int Count)>();
            foreach (var entry in entries.OrderByDescending(entry => entry.StartedAt))
            {
                if (groups.Count > 0 && BackupsViewModel.TitleOf(groups[^1].Latest) == BackupsViewModel.TitleOf(entry))
                    groups[^1] = (groups[^1].Latest, groups[^1].Count + 1);
                else if (groups.Count == 4)
                    break;
                else
                    groups.Add((entry, 1));
            }
            RecentChanges = groups.Select(group => new RecentChange(
                    BackupsViewModel.TitleOf(group.Latest),
                    BackupsViewModel.AreaOf(group.Latest.TweakId) + (group.Count > 1 ? $" · {Plural.Of(group.Count, "registro", "registros")}" : string.Empty),
                    group.Latest.StartedAt.LocalDateTime.ToString("dd/MM HH:mm"),
                    group.Latest.Outcome,
                    group.Latest.Outcome switch
                    {
                        "Sucesso verificado" or "Reversão verificada" => "Verificado",
                        "Executado sem verificação" => "Executado",
                        var outcome => outcome
                    },
                    group.Latest.Outcome is "Sucesso verificado" or "Reversão verificada"))
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

    private async Task LoadGamesAsync()
    {
        var found = await Task.Run(() => SupportedGames.All.Select(_games.Locate).Where(game => game.IsInstalled).ToArray());
        var steamRoot = _games.Steam.SteamRoot;
        // Ícones locais (os mesmos da biblioteca de Jogos), criados na thread da interface.
        InstalledGames = found.Select(game => new HomeGame(game.Game.Name, PlatformName(game.Game.Platform),
                game.Game.SteamAppId is { } appId ? LocalArtwork.SteamAppIcon(steamRoot, appId) : LocalArtwork.ExecutableIcon(game.ExecutablePath)))
            .ToArray();
        OnPropertyChanged(nameof(InstalledGames));
        OnPropertyChanged(nameof(HasInstalledGames));
    }

    private static string PlatformName(GamePlatform platform) => platform switch
    {
        GamePlatform.Steam => "Steam",
        GamePlatform.Epic => "Epic Games",
        GamePlatform.Riot => "Riot Client",
        _ => "Windows"
    };

    private void OnSourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsCleanupUnscanned));
        OnPropertyChanged(nameof(IsTweaksLoading));
        OnPropertyChanged(nameof(HasRecommendedPending));
        OnPropertyChanged(nameof(RecommendedMissing));
        OnPropertyChanged(nameof(TweaksBadge));
        OnPropertyChanged(nameof(TweaksText));
        OnPropertyChanged(nameof(CleanupBadge));
        OnPropertyChanged(nameof(StartupBadge));
    }
}
