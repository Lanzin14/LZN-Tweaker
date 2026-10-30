using System.Windows.Controls;
using LznTweaker.ViewModels;

namespace LznTweaker.Views;

/// <summary>Limpeza do sistema. Varredura, confirmação e limpeza ficam no <see cref="CleanupViewModel"/>.</summary>
public partial class CleanupView : UserControl
{
    public CleanupView(CleanupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.OnShownAsync();
    }
}
