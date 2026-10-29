using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
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
        DataContext = this;
        UpdateSummary();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<TempCleanupPreviewItem> Items { get; }
    public IReadOnlyList<TempCleanupFile> SelectedFiles => Items.Where(item => item.IsSelected).Select(item => item.File).ToArray();
    public string SelectionSummary { get; private set; } = "";

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Items)
            item.IsSelected = true;
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Items)
            item.IsSelected = false;
    }

    private void Accept_Click(object sender, RoutedEventArgs e) => DialogResult = true;

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
    }

    private static string FormatBytes(long bytes) => bytes >= 1_073_741_824
        ? $"{bytes / 1_073_741_824d:0.00} GB"
        : bytes >= 1_048_576
            ? $"{bytes / 1_048_576d:0.0} MB"
            : $"{bytes / 1_024d:0} KB";
}
