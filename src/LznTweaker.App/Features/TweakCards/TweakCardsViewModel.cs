using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LznTweaker.Core.Maintenance;
using LznTweaker.Core.Tweaks;
using LznTweaker.Infrastructure;
using LznTweaker.Shell;

namespace LznTweaker.Features.TweakCards;

/// <summary>Dependências comuns das páginas de cards, agrupadas para manter os construtores curtos.</summary>
public sealed record TweakCardsServices(
    TweakBatchExecutor Executor,
    TweakOperationHistory History,
    RestorePointPrompt RestorePointPrompt,
    SystemToolsService Tools,
    IUserDialogs Dialogs,
    INotificationService Notifications,
    INavigationService Navigation);

/// <summary>
/// Página de cards (General, Network, Fixes): abas, busca instantânea, filtros por risco e estado, ordenação,
/// seleção pendente com revisão antes de aplicar, painel de detalhes e ações imediatas.
/// </summary>
public partial class TweakCardsViewModel : ObservableObject
{
    public const string AllRisks = "Todos os riscos";
    public const string AllStates = "Todos os estados";
    private readonly TweakCardsServices _services;
    private IReadOnlyDictionary<string, TweakOperationResult> _undoCandidates = new Dictionary<string, TweakOperationResult>();
    private bool _initialized;

    public TweakCardsViewModel(TweakCardsPage page, TweakCardsServices services)
    {
        _services = services;
        Title = page.Title;
        Guide = page.Guide;
        var recommended = TweakPresetCatalog.GetSelection(TweakPreset.Standard);
        foreach (var tab in page.Tabs)
        {
            Tabs.Add(new CardTabViewModel(tab.Key, tab.Title, tab.Glyph, tab.Description, tab.Highlight));
            foreach (var id in tab.TweakIds)
            {
                var definition = TweakCatalog.GetById(id);
                var card = new TweakCardViewModel(definition, tab.Key, TweakCardPages.GlyphFor(id, tab.Glyph), recommended.Contains(id));
                card.PendingChanged += _ => OnPendingChanged();
                Cards.Add(card);
            }
            foreach (var action in tab.Actions)
                Cards.Add(new QuickActionCardViewModel(action, tab.Key));
            foreach (var guide in tab.Guides ?? [])
                Cards.Add(new GuideCardViewModel(guide, tab.Key));
        }
        foreach (var tab in Tabs)
            tab.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(CardTabViewModel.IsSelected) && sender is CardTabViewModel { IsSelected: true } selected)
                {
                    SelectedTabDescription = selected.Description;
                    SelectedTabHighlight = selected.Highlight;
                    RefreshView();
                }
            };
        Tabs[0].IsSelected = true;

        CardsView = CollectionViewSource.GetDefaultView(Cards);
        CardsView.Filter = item => item is CardViewModel card && Matches(card);
        ((ListCollectionView)CardsView).CustomSort = new CardComparer(this);
    }

    public string Title { get; }
    public CategoryGuide Guide { get; }

    [ObservableProperty]
    private string _selectedTabDescription = "";

    [ObservableProperty]
    private string? _selectedTabHighlight;
    public ObservableCollection<CardTabViewModel> Tabs { get; } = [];
    public ObservableCollection<CardViewModel> Cards { get; } = [];
    public ICollectionView CardsView { get; }
    public IReadOnlyList<string> RiskOptions { get; } = [AllRisks, "Baixo risco", "Atenção", "Alto risco"];
    public IReadOnlyList<string> StateOptions { get; } = [AllStates, "Ativos", "Inativos", "Pendentes", "Recomendados", "Não verificáveis"];
    public IReadOnlyList<string> SortOptions { get; } = ["Relevância", "Alfabética"];
    public bool HasTweaks => Cards.OfType<TweakCardViewModel>().Any();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _riskFilter = AllRisks;

    [ObservableProperty]
    private string _stateFilter = AllStates;

    [ObservableProperty]
    private string _sortMode = "Relevância";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand), nameof(RefreshCommand), nameof(DiscardCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private string? _loadError;

    [ObservableProperty]
    private string _statusText = "Lendo o estado atual dos ajustes…";

    [ObservableProperty]
    private CardViewModel? _selectedCard;

    [ObservableProperty]
    private int _visibleCount;

    public int PendingCount => Cards.Count(card => card.IsPending);
    public bool HasPending => PendingCount > 0;
    public bool HasFilters => RiskFilter != AllRisks || StateFilter != AllStates;
    public string PendingSummary
    {
        get
        {
            var tweaks = Cards.OfType<TweakCardViewModel>().Where(card => card.IsPending).ToArray();
            var undo = tweaks.Count(card => card.PendingIsUndo);
            return $"{tweaks.Length} alteração(ões) pendente(s)" + (undo > 0 ? $" · {undo} para desfazer" : string.Empty) +
                   (tweaks.Any(card => card.Definition.RequiresRestart) ? " · inclui itens que exigem reinicialização" : string.Empty);
        }
    }

    partial void OnSearchTextChanged(string value) => RefreshView();
    partial void OnRiskFilterChanged(string value) { RefreshView(); OnPropertyChanged(nameof(HasFilters)); }
    partial void OnStateFilterChanged(string value) { RefreshView(); OnPropertyChanged(nameof(HasFilters)); }
    partial void OnSortModeChanged(string value) => RefreshView();

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;
        _initialized = true;
        await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanWork))]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        IsLoading = true;
        LoadError = null;
        try
        {
            var tweaks = Cards.OfType<TweakCardViewModel>().ToArray();
            var states = await Task.Run(() => tweaks.ToDictionary(card => card.Id, card => TweakStateDetector.Detect(card.Definition)));
            _undoCandidates = TweakStateDetector.GetUndoCandidates(await _services.History.ReadAsync());
            foreach (var card in tweaks)
            {
                var (state, detail) = states[card.Id];
                card.SetDetected(state, detail, _undoCandidates.ContainsKey(card.Id));
            }
            var active = tweaks.Count(card => card.IsActive);
            StatusText = tweaks.Length == 0
                ? "Ferramentas do Windows; cada ação pede confirmação quando altera algo."
                : $"{active} de {tweaks.Length} ajustes ativos · estado lido do sistema às {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            LoadError = $"Não foi possível ler o estado: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            IsBusy = false;
            OnPendingChanged();
            RefreshView();
        }
    }

    [RelayCommand]
    private void SelectTab(CardTabViewModel? tab)
    {
        if (tab is null)
            return;
        foreach (var candidate in Tabs)
            candidate.IsSelected = ReferenceEquals(candidate, tab);
    }

    [RelayCommand]
    private void ClearFilters()
    {
        RiskFilter = AllRisks;
        StateFilter = AllStates;
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void OpenDetails(CardViewModel? card) => SelectedCard = card;

    [RelayCommand]
    private void CloseDetails() => SelectedCard = null;

    [RelayCommand]
    private void ToggleQueue(TweakCardViewModel? card)
    {
        if (card is { IsAction: true })
            card.IsQueued = !card.IsQueued;
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Discard()
    {
        foreach (var card in Cards.OfType<TweakCardViewModel>())
            card.Discard();
        OnPendingChanged();
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        var pending = Cards.OfType<TweakCardViewModel>().Where(card => card.IsPending).ToArray();
        var applies = pending.Where(card => !card.PendingIsUndo).ToArray();
        var undos = pending.Where(card => card.PendingIsUndo).ToArray();
        if (pending.Length == 0)
            return;
        if (!_services.Dialogs.Confirm(BuildReview(applies, undos), "Revisar alterações"))
            return;
        if (applies.Length > 0 && !await _services.RestorePointPrompt.EnsureAvailableAsync())
            return;

        IsBusy = true;
        _services.Notifications.SetStatus("Aplicando alterações…");
        var results = new List<TweakOperationResult>();
        try
        {
            if (applies.Length > 0)
            {
                var progress = new Progress<TweakBatchProgress>(update =>
                    StatusText = update.Completed == 0 ? update.Message : $"{update.Completed}/{update.Total} · {update.Name}: {update.Outcome}");
                results.AddRange(await _services.Executor.RunAsync(applies.Select(card => card.Definition).ToArray(), [], progress));
            }
            if (undos.Length > 0)
            {
                StatusText = "Desfazendo os itens marcados…";
                var entries = undos.Select(card => _undoCandidates.GetValueOrDefault(card.Id)).OfType<TweakOperationResult>().ToArray();
                results.AddRange(await _services.Executor.UndoAsync(entries));
            }
        }
        finally
        {
            IsBusy = false;
            _services.Notifications.SetStatus("Pronto");
        }

        var ok = results.Count(result => result.Outcome is "Sucesso verificado" or "Reversão verificada" or "Executado sem verificação"
            && result.TweakId != TweakCatalog.CreateRestorePointId);
        var blocked = results.Any(result => result.Outcome == "Bloqueado");
        var failures = results.Where(result => result.Outcome is not ("Sucesso verificado" or "Reversão verificada" or "Executado sem verificação"
            or "Nada a alterar" or "Ponto recente reutilizado")).ToArray();
        _services.Notifications.Notify(
            blocked ? "Lote bloqueado" : failures.Length == 0 ? "Alterações concluídas" : "Concluído com falhas",
            blocked
                ? "O ponto de restauração não foi criado; nenhuma alteração foi feita."
                : $"{ok} item(ns) concluído(s)" + (failures.Length == 0 ? "." : $"; {failures.Length} com falha: " +
                    string.Join(" | ", failures.Take(3).Select(result => $"{result.RequestedState}: {result.Message}"))) +
                  (pending.Any(card => card.Definition.RequiresRestart) ? " Alguns itens exigem reinicialização." : string.Empty),
            blocked || failures.Length > 0 ? NotificationKind.Warning : NotificationKind.Success);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RunQuickActionAsync(QuickActionCardViewModel? card)
    {
        if (card is null || card.IsRunning)
            return;
        if (card.Definition.NavigateTo is { } page)
        {
            _services.Navigation.Navigate(page);
            return;
        }
        if (card.Definition.Tool is not { } tool)
            return;
        if (card.Definition.Confirmation is { } confirmation && !_services.Dialogs.Confirm(confirmation, card.Title))
            return;

        card.IsRunning = true;
        try
        {
            var result = await _services.Tools.RunAsync(tool);
            card.LastResult = result.Outcome switch
            {
                "Sucesso verificado" => $"Concluído às {DateTime.Now:HH:mm}",
                "Executado sem verificação" => "Aberto",
                _ => "Falhou"
            };
            _services.Notifications.Notify(card.Title, result.Message,
                result.Outcome == "Falha ou não verificado" ? NotificationKind.Error : NotificationKind.Success);
        }
        finally
        {
            card.IsRunning = false;
        }
    }

    private bool CanWork() => !IsBusy;
    private bool CanApply() => !IsBusy && HasPending;

    private void OnPendingChanged()
    {
        OnPropertyChanged(nameof(PendingCount));
        OnPropertyChanged(nameof(HasPending));
        OnPropertyChanged(nameof(PendingSummary));
        ApplyCommand.NotifyCanExecuteChanged();
        DiscardCommand.NotifyCanExecuteChanged();
        if (StateFilter == "Pendentes")
            RefreshView();
    }

    private void RefreshView()
    {
        CardsView?.Refresh();
        VisibleCount = Cards.Count(card => Matches(card));
    }

    private bool Matches(CardViewModel card)
    {
        var search = SearchText.Trim();
        // Com busca, procura em todas as abas; sem busca, só na aba atual.
        if (search.Length > 0)
        {
            if (!card.SearchText.Contains(search, StringComparison.CurrentCultureIgnoreCase))
                return false;
        }
        else if (Tabs.FirstOrDefault(tab => tab.IsSelected)?.Key is { } tab && card.TabKey != tab)
        {
            return false;
        }

        if (RiskFilter != AllRisks && card.RiskText != RiskFilter)
            return false;
        return StateFilter switch
        {
            "Ativos" => card.IsActive,
            "Inativos" => card is TweakCardViewModel { IsToggle: true, IsActive: false },
            "Pendentes" => card.IsPending,
            "Recomendados" => card.IsRecommended,
            "Não verificáveis" => card is TweakCardViewModel { IsVerifiable: false },
            _ => true
        };
    }

    private static string BuildReview(IReadOnlyList<TweakCardViewModel> applies, IReadOnlyList<TweakCardViewModel> undos)
    {
        var text = new StringBuilder();
        if (applies.Count > 0)
        {
            text.AppendLine("Aplicar:");
            foreach (var card in applies)
                text.AppendLine($"• {card.Title} — {card.RiskText}{(card.Definition.RequiresRestart ? " · requer reinicialização" : string.Empty)}" +
                                $"{(card.Definition.HasReliableRollback ? string.Empty : " · sem reversão automática")}");
            text.AppendLine();
        }
        if (undos.Count > 0)
        {
            text.AppendLine("Desfazer (restaurar o valor anterior registrado):");
            foreach (var card in undos)
                text.AppendLine($"• {card.Title}");
            text.AppendLine();
        }
        if (applies.Count > 0)
            text.AppendLine("Antes de aplicar, um ponto de restauração é criado (obrigatório). Se não puder ser criado, nada é alterado.");
        text.AppendLine("Cada item é verificado depois e registrado em Backups. Nenhum reinício é feito automaticamente.");
        text.AppendLine();
        text.Append("Continuar?");
        return text.ToString();
    }

    /// <summary>Relevância: pendentes, recomendados, menor risco e nome; ou ordem alfabética.</summary>
    private sealed class CardComparer(TweakCardsViewModel owner) : IComparer
    {
        public int Compare(object? x, object? y)
        {
            if (x is not CardViewModel a || y is not CardViewModel b)
                return 0;
            if (owner.SortMode == "Alfabética")
                return string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase);
            // Guias manuais seguem a ordem do catálogo (um passo a passo pensado em sequência).
            if (a is GuideCardViewModel && b is GuideCardViewModel)
                return owner.Cards.IndexOf(a).CompareTo(owner.Cards.IndexOf(b));
            var order = b.IsPending.CompareTo(a.IsPending);
            if (order == 0) order = b.IsRecommended.CompareTo(a.IsRecommended);
            if (order == 0) order = (a is QuickActionCardViewModel).CompareTo(b is QuickActionCardViewModel);
            if (order == 0) order = a.Risk.CompareTo(b.Risk);
            return order != 0 ? order : string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}

public sealed class GeneralPageViewModel(TweakCardsServices services) : TweakCardsViewModel(TweakCardPages.General, services);
public sealed class NetworkPageViewModel(TweakCardsServices services) : TweakCardsViewModel(TweakCardPages.Network, services);
public sealed class FixesPageViewModel(TweakCardsServices services) : TweakCardsViewModel(TweakCardPages.Fixes, services);
