using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LznTweaker.Core.Cleanup;
using LznTweaker.Core.Debloat;
using LznTweaker.Core.GameOptimization;
using LznTweaker.Core.GameTuning;
using LznTweaker.Core.Maintenance;
using LznTweaker.Core.Tweaks;
using LznTweaker.Infrastructure;
using LznTweaker.Shell;

namespace LznTweaker.Features.Backups;

public sealed partial class HistoryEntryViewModel(TweakOperationResult entry, string area, string? restorePage, bool canRestoreHere) : ObservableObject
{
    private static SolidColorBrush Green => ThemeBrushes.Success;
    private static SolidColorBrush Amber => ThemeBrushes.Warning;
    private static SolidColorBrush Red => ThemeBrushes.Danger;
    private static SolidColorBrush Gray => ThemeBrushes.Muted;

    public TweakOperationResult Entry { get; } = entry;
    public string When => Entry.StartedAt.LocalDateTime.ToString("g");
    public string Title => Entry.RequestedState;
    public string Area { get; } = area;
    public string Outcome => Entry.Outcome;
    public string Message => Entry.Message;
    public string PreviousState => $"Antes: {Entry.PreviousState}";
    public string? RestorePage { get; } = restorePage;
    public bool CanRestoreHere { get; } = canRestoreHere;
    public bool HasRestoreElsewhere => !CanRestoreHere && RestorePage is not null;
    public string RestoreHint => CanRestoreHere ? "Valor anterior registrado: pode ser restaurado aqui." : RestorePage is null ? string.Empty : "A restauração deste item fica na página de origem.";

    [ObservableProperty]
    private bool _isSelected;

    public Brush OutcomeBrush => Entry.Outcome switch
    {
        "Sucesso verificado" or "Reversão verificada" or "Ponto recente reutilizado" => Green,
        "Executado sem verificação" or "Nada a alterar" or "Requer Microsoft Store" => Gray,
        "Bloqueado" or "Conflito" or "Reversão recusada" => Amber,
        _ => Red
    };

}

/// <summary>
/// Histórico de todas as operações (o registro local do app), com busca, filtros e restauração: ajustes do catálogo
/// são restaurados aqui (individualmente ou em lote); apps, pastas da Steam e o perfil do Deadlock na página de origem.
/// </summary>
public sealed partial class BackupsViewModel : ObservableObject
{
    private const string AllOutcomes = "Todos os resultados";
    private readonly TweakOperationHistory _history;
    private readonly TweakBatchExecutor _executor;
    private readonly IUserDialogs _dialogs;
    private readonly INotificationService _notifications;
    private readonly INavigationService _navigation;

    public BackupsViewModel(TweakOperationHistory history, TweakBatchExecutor executor, IUserDialogs dialogs,
        INotificationService notifications, INavigationService navigation)
    {
        _history = history;
        _executor = executor;
        _dialogs = dialogs;
        _notifications = notifications;
        _navigation = navigation;
        EntriesView = CollectionViewSource.GetDefaultView(Entries);
        EntriesView.Filter = item => item is HistoryEntryViewModel entry && Matches(entry);
    }

    public ObservableCollection<HistoryEntryViewModel> Entries { get; } = [];
    public ICollectionView EntriesView { get; }
    public IReadOnlyList<string> OutcomeOptions { get; } = [AllOutcomes, "Concluídas", "Falhas", "Bloqueadas ou recusadas", "Restauráveis"];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _outcomeFilter = AllOutcomes;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreSelectedCommand), nameof(RefreshCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _summary = "Carregando o histórico…";

    [ObservableProperty]
    private int _visibleCount;

    partial void OnSearchTextChanged(string value) => RefreshView();
    partial void OnOutcomeFilterChanged(string value) => RefreshView();

    [RelayCommand(CanExecute = nameof(CanWork))]
    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var entries = await _history.ReadAsync();
            var undo = TweakStateDetector.GetUndoCandidates(entries);
            Entries.Clear();
            foreach (var entry in entries.Reverse())
            {
                var isUndoCandidate = undo.TryGetValue(entry.TweakId, out var candidate) && candidate.OperationId == entry.OperationId;
                Entries.Add(new HistoryEntryViewModel(entry, AreaOf(entry.TweakId), RestorePageOf(entry), isUndoCandidate));
            }
            Summary = $"{entries.Count} operação(ões) registradas · {undo.Count} ajuste(s) com valor anterior guardado · " +
                      $"{entries.Count(entry => entry.Outcome is not ("Sucesso verificado" or "Reversão verificada" or "Executado sem verificação" or "Nada a alterar" or "Ponto recente reutilizado"))} com falha ou bloqueio";
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            Summary = $"O histórico não pôde ser lido: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            RefreshView();
        }
    }

    [RelayCommand(CanExecute = nameof(CanWork))]
    private async Task RestoreSelectedAsync()
    {
        var selected = Entries.Where(entry => entry.IsSelected && entry.CanRestoreHere).ToArray();
        if (selected.Length == 0)
        {
            _dialogs.ShowInfo("Marque os ajustes com valor anterior registrado que deseja restaurar.", "Restaurar");
            return;
        }
        await RestoreAsync(selected);
    }

    [RelayCommand]
    private async Task RestoreOneAsync(HistoryEntryViewModel? entry)
    {
        if (entry is { CanRestoreHere: true })
            await RestoreAsync([entry]);
    }

    [RelayCommand]
    private void OpenOrigin(HistoryEntryViewModel? entry)
    {
        if (entry?.RestorePage is { } page)
            _navigation.Navigate(page);
    }

    private async Task RestoreAsync(IReadOnlyList<HistoryEntryViewModel> entries)
    {
        if (!_dialogs.Confirm(
                "Restaurar o valor anterior registrado de:\n\n" + string.Join("\n", entries.Select(entry => $"• {entry.Title} ({entry.When})")) +
                "\n\nA restauração só é aplicada se o valor atual ainda for o que o LznTweaker gravou; se você ou outro programa mudou depois, " +
                "aquele item é preservado.\n\nContinuar?", "Restaurar ajustes"))
            return;

        IsBusy = true;
        try
        {
            var results = await _executor.UndoAsync(entries.Select(entry => entry.Entry).ToArray());
            var ok = results.Count(result => result.Outcome == "Reversão verificada");
            _notifications.Notify(ok == results.Count ? "Restauração concluída" : "Restauração parcial",
                $"{ok} de {results.Count} item(ns) restaurado(s)." + (ok == results.Count ? string.Empty :
                    " " + string.Join(" | ", results.Where(result => result.Outcome != "Reversão verificada").Take(3).Select(result => result.Message))),
                ok == results.Count ? NotificationKind.Success : NotificationKind.Warning);
        }
        finally
        {
            IsBusy = false;
        }
        await RefreshAsync();
    }

    private bool CanWork() => !IsBusy;

    private void RefreshView()
    {
        EntriesView.Refresh();
        VisibleCount = Entries.Count(Matches);
    }

    private bool Matches(HistoryEntryViewModel entry)
    {
        var search = SearchText.Trim();
        if (search.Length > 0 && !$"{entry.Title} {entry.Message} {entry.Area}".Contains(search, StringComparison.CurrentCultureIgnoreCase))
            return false;
        return OutcomeFilter switch
        {
            "Concluídas" => entry.Outcome is "Sucesso verificado" or "Reversão verificada" or "Executado sem verificação" or "Ponto recente reutilizado",
            "Falhas" => entry.Outcome == "Falha ou não verificado",
            "Bloqueadas ou recusadas" => entry.Outcome is "Bloqueado" or "Conflito" or "Reversão recusada",
            "Restauráveis" => entry.CanRestoreHere || entry.HasRestoreElsewhere,
            _ => true
        };
    }

    private static string AreaOf(string tweakId) => tweakId switch
    {
        _ when tweakId.StartsWith(AppxDebloatService.OperationPrefix, StringComparison.Ordinal) => "Remover apps",
        _ when tweakId.StartsWith(SteamFolderBackupService.OperationPrefix, StringComparison.Ordinal) => "Jogos · Steam",
        _ when tweakId.StartsWith(SystemToolsService.OperationPrefix, StringComparison.Ordinal) => "Ferramentas",
        _ when tweakId.StartsWith(SystemCleanupService.OperationPrefix, StringComparison.Ordinal) => "Limpeza",
        GameTuningService.DeadlockOperationId => "Jogos · Deadlock",
        ShaderCacheService.OperationId => "Jogos · Windows",
        _ => "Ajustes"
    };

    private static string? RestorePageOf(TweakOperationResult entry) => entry.RollbackData is null && entry.TweakId != GameTuningService.DeadlockOperationId
        ? null
        : entry.TweakId switch
        {
            _ when entry.TweakId.StartsWith(AppxDebloatService.OperationPrefix, StringComparison.Ordinal) => ShellPages.Debloat,
            _ when entry.TweakId.StartsWith(SteamFolderBackupService.OperationPrefix, StringComparison.Ordinal) => ShellPages.Games,
            GameTuningService.DeadlockOperationId when entry.Outcome == "Sucesso verificado" => ShellPages.Games,
            _ => null
        };
}
