using System.Windows;
using System.Windows.Controls;
using LznTweaker.Core.Cleanup;
using LznTweaker.ViewModels;

namespace LznTweaker.Views;

public partial class CleanupView : UserControl
{
    private readonly CleanupViewModel _viewModel;

    public CleanupView()
    {
        InitializeComponent();
        _viewModel = new CleanupViewModel(new SystemCleanupService());
        DataContext = _viewModel;

        Loaded += async (_, _) =>
        {
            if (_viewModel.Categories.All(c => c.ScanResult is null))
            {
                await _viewModel.ScanAllAsync();
            }
        };
    }

    private async void Scan_Click(object sender, RoutedEventArgs e) =>
        await _viewModel.ScanAllAsync();

    private async void Clean_Click(object sender, RoutedEventArgs e)
    {
        var choice = MessageBox.Show(
            $"Deseja prosseguir com a remoção de {_viewModel.SelectedSizeText}?\n\n" +
            "• Arquivos temporários e caches serão apagados definitivamente.\n" +
            "• Arquivos que estiverem em uso por programas abertos no momento serão ignorados e preservados sem interrupções.\n" +
            "• A Lixeira será esvaziada caso selecionada.\n\n" +
            "Deseja continuar?",
            "Confirmar Limpeza de Sistema",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (choice == MessageBoxResult.Yes)
        {
            await _viewModel.CleanSelectedAsync();
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) => _viewModel.SelectAll(true);

    private void SelectNone_Click(object sender, RoutedEventArgs e) => _viewModel.SelectAll(false);
}
