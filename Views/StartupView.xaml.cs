using System.Windows;
using System.Windows.Controls;
using LznTweaker.Core.Startup;
using LznTweaker.ViewModels;

namespace LznTweaker.Views;

public partial class StartupView : UserControl
{
    private readonly StartupViewModel _viewModel;

    public StartupView()
    {
        InitializeComponent();
        _viewModel = new StartupViewModel(new StartupManagerService());
        DataContext = _viewModel;

        Loaded += async (_, _) =>
        {
            if (_viewModel.TotalCount == 0)
            {
                await _viewModel.RefreshAsync();
            }
        };
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) =>
        await _viewModel.RefreshAsync();

    private void DisableLaunchers_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Isso desativará a inicialização automática de clientes e launchers (como Steam, Epic Games, Discord, Riot, etc.).\n\n" +
            "• Os jogos continuarão funcionando normalmente quando você os abrir manualmente.\n" +
            "• A inicialização do Windows ficará mais rápida e consumirá menos memória RAM de fundo.\n\n" +
            "Deseja desativar esses itens da inicialização?",
            "Otimizar Inicialização para Jogos",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.Yes);

        if (result == MessageBoxResult.Yes)
        {
            _viewModel.DisableAllLaunchers();
        }
    }
}
