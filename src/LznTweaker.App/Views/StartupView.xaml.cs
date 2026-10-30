using System.Windows.Controls;
using LznTweaker.ViewModels;

namespace LznTweaker.Views;

/// <summary>Programas da inicialização. Leitura, confirmação e alterações ficam no <see cref="StartupViewModel"/>.</summary>
public partial class StartupView : UserControl
{
    public StartupView(StartupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.OnShownAsync();
    }
}
