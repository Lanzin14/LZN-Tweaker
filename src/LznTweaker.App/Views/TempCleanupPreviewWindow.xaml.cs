using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using LznTweaker.Core.Tweaks;

namespace LznTweaker.Views;

public sealed class TempCleanupPreviewItem(TempCleanupFile file) : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public TempCleanupFile File { get; } = file;
    public string DisplayName => File.DisplayName;
    public string FullPath => File.FullPath;
    public string SizeText => File.SizeText;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class TempCleanupPreviewWindow : Window, INotifyPropertyChanged
{
    public TempCleanupPreviewWindow(IReadOnlyList<TempCleanupFile> files)
    {
        InitializeComponent();
        Items = new ObservableCollection<TempCleanupPreviewItem>(files.Select(file => new TempCleanupPreviewItem(file)));
        foreach (var item in Items)
            item.PropertyChanged += Item_PropertyChanged;
        SelectAllCommand = new RelayCommand(() => SetAll(true));
        SelectNoneCommand = new RelayCommand(() => SetAll(false));
        AcceptCommand = new RelayCommand(() => DialogResult = true, () => Items.Any(item => item.IsSelected));
        DataContext = this;
        UpdateSummary();
    }

    public IRelayCommand SelectAllCommand { get; }
    public IRelayCommand SelectNoneCommand { get; }
    /// <summary>Usar a seleção: só habilita com ao menos um arquivo marcado.</summary>
    public IRelayCommand AcceptCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<TempCleanupPreviewItem> Items { get; }
    public IReadOnlyList<TempCleanupFile> SelectedFiles => Items.Where(item => item.IsSelected).Select(item => item.File).ToArray();
    public string SelectionSummary { get; private set; } = "";

    private void SetAll(bool selected)
    {
        foreach (var item in Items)
            item.IsSelected = selected;
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TempCleanupPreviewItem.IsSelected))
            UpdateSummary();
    }

    private void UpdateSummary()
    {
        var selected = Items.Where(item => item.IsSelected).Select(item => item.File).ToArray();
        var size = selected.Sum(file => file.Length);
        SelectionSummary = $"{selected.Length:N0} de {Items.Count:N0} arquivo(s) · {FormatBytes(size)} selecionados";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectionSummary)));
        AcceptCommand?.NotifyCanExecuteChanged();
    }

    private static string FormatBytes(long bytes) => bytes >= 1_073_741_824
        ? $"{bytes / 1_073_741_824d:0.00} GB"
        : bytes >= 1_048_576
            ? $"{bytes / 1_048_576d:0.0} MB"
            : $"{bytes / 1_024d:0} KB";
}
