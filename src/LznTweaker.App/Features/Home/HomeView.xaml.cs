using System.Windows.Controls;

namespace LznTweaker.Features.Home;

public partial class HomeView : UserControl
{
    public HomeView(HomeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        // Atualiza só enquanto a Home está visível, para não consultar WMI e processos em segundo plano.
        Loaded += async (_, _) => await viewModel.OnShownAsync();
        Unloaded += (_, _) => viewModel.OnHidden();
    }
}
