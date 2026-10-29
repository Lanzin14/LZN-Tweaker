using System.Windows.Controls;

namespace LznTweaker.Features.GameProfiles;

public partial class GameLibraryView : UserControl
{
    public GameLibraryView(GameLibraryViewModel viewModel, DeadlockTuningView deadlockTuningView)
    {
        InitializeComponent();
        DataContext = viewModel;
        // A página do gameinfo.gi continua sendo a mesma instância (e o mesmo estado) de antes.
        DeadlockProfileHost.Content = deadlockTuningView;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }
}
