using System.Windows.Controls;

namespace LznTweaker.Features.Debloat;

public partial class AppDebloatView : UserControl
{
    public AppDebloatView(AppDebloatViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }
}
