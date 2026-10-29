using System.Windows.Controls;

namespace LznTweaker.Features.GameProfiles;

public partial class DeadlockTuningView : UserControl
{
    public DeadlockTuningView(DeadlockTuningViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }
}
