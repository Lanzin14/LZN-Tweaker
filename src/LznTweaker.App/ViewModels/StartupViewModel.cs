using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Media;
using LznTweaker.Core.Startup;

namespace LznTweaker.ViewModels;

public sealed class StartupItemViewModel : INotifyPropertyChanged
{
    private static readonly SolidColorBrush GreenBrush = new((Color)ColorConverter.ConvertFromString("#6EE7B7"));
    private static readonly SolidColorBrush AmberBrush = new((Color)ColorConverter.ConvertFromString("#F5C46B"));
    private static readonly SolidColorBrush RedBrush = new((Color)ColorConverter.ConvertFromString("#F87171"));
    private static readonly SolidColorBrush GrayBrush = new((Color)ColorConverter.ConvertFromString("#8A8A96"));

    private static readonly SolidColorBrush GreenBg = new((Color)ColorConverter.ConvertFromString("#10251C"));
    private static readonly SolidColorBrush AmberBg = new((Color)ColorConverter.ConvertFromString("#2A2112"));
    private static readonly SolidColorBrush RedBg = new((Color)ColorConverter.ConvertFromString("#2A1416"));

    private readonly StartupManagerService _service;
    private bool _isEnabled;

    public StartupItemViewModel(StartupItem item, StartupManagerService service)
    {
        Item = item;
        _service = service;
        _isEnabled = item.IsEnabled;
    }

    public StartupItem Item { get; }

    public string Name => Item.Name;
    public string Command => Item.Command;
    public string Publisher => Item.Publisher;
    public string LocationDisplay => Item.LocationDisplay;
    public string CategoryDisplay => Item.CategoryDisplay;
    public string CategoryGlyph => Item.CategoryGlyph;
    public StartupCategory Category => Item.Category;
    public string Impact => Item.Impact;

    public string StateText => IsEnabled ? "Ativado" : "Desativado";
    public Brush StateBrush => IsEnabled ? GreenBrush : GrayBrush;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            var success = _service.SetItemState(Item, value);
            if (success)
            {
                _isEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StateText));
                OnPropertyChanged(nameof(StateBrush));
                OnStateChanged?.Invoke();
            }
        }
    }

    public Action? OnStateChanged { get; set; }

    public Brush ImpactBrush => Item.Impact switch
    {
        "Alto" => RedBrush,
        "Médio" => AmberBrush,
        _ => GreenBrush
    };

    public Brush ImpactBgBrush => Item.Impact switch
    {
        "Alto" => RedBg,
        "Médio" => AmberBg,
        _ => GreenBg
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
}

public sealed class StartupViewModel : INotifyPropertyChanged
{
    private readonly StartupManagerService _service;
    private readonly ObservableCollection<StartupItemViewModel> _allItems = [];
    private string _searchText = "";
    private string _selectedCategoryFilter = "Todos";
    private bool _isBusy;
    private string _statusMessage = "Carregando programas de inicialização…";

    public StartupViewModel(StartupManagerService service)
    {
        _service = service;
        FilteredView = CollectionViewSource.GetDefaultView(_allItems);
        FilteredView.Filter = FilterPredicate;

        // Os textos precisam ser idênticos a StartupItem.CategoryDisplay para o filtro funcionar.
        CategoryFilters = ["Todos", "Launcher de Jogos", "Periféricos & Hardware", "Comunicação & Chat", "Mídia & Áudio", "Nuvem & Sincronização", "Utilitário do Sistema"];
    }

    public ICollectionView FilteredView { get; }
    public IReadOnlyList<string> CategoryFilters { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SearchPlaceholderVisibility));
            FilteredView.Refresh();
        }
    }

    public System.Windows.Visibility SearchPlaceholderVisibility =>
        string.IsNullOrEmpty(SearchText) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public string SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (_selectedCategoryFilter == value) return;
            _selectedCategoryFilter = value;
            OnPropertyChanged();
            FilteredView.Refresh();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
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

    public int TotalCount => _allItems.Count;
    public int EnabledCount => _allItems.Count(i => i.IsEnabled);
    public int DisabledCount => _allItems.Count(i => !i.IsEnabled);

    public string SummaryText => $"{EnabledCount} ativados · {DisabledCount} desativados ({TotalCount} totais)";

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        StatusMessage = "Consultando registro e pastas de inicialização do Windows…";

        try
        {
            var items = await _service.GetStartupItemsAsync(cancellationToken);
            _allItems.Clear();
            foreach (var item in items)
            {
                var vm = new StartupItemViewModel(item, _service)
                {
                    OnStateChanged = UpdateCounters
                };
                _allItems.Add(vm);
            }

            StatusMessage = $"Lista atualizada: {SummaryText}.";
            UpdateCounters();
            FilteredView.Refresh();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Não foi possível carregar a inicialização: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void DisableAllLaunchers()
    {
        foreach (var item in _allItems.Where(i => i.Category is StartupCategory.GameLauncher or StartupCategory.CommunicationAndChat))
        {
            item.IsEnabled = false;
        }
        UpdateCounters();
    }

    private bool FilterPredicate(object obj)
    {
        if (obj is not StartupItemViewModel item) return false;

        // Filtro por Categoria
        if (SelectedCategoryFilter != "Todos" && !item.CategoryDisplay.Equals(SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        // Filtro por Texto de Busca
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var matchName = item.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            var matchPub = item.Publisher.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            var matchCmd = item.Command.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            if (!matchName && !matchPub && !matchCmd) return false;
        }

        return true;
    }

    private void UpdateCounters()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(EnabledCount));
        OnPropertyChanged(nameof(DisabledCount));
        OnPropertyChanged(nameof(SummaryText));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
}
