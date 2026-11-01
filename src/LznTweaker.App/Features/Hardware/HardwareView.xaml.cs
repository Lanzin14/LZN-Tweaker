using System.Windows.Controls;
using LznTweaker.Core.GameOptimization;
using LznTweaker.Features.GameProfiles;
using LznTweaker.ViewModels;

namespace LznTweaker.Features.Hardware;

public sealed class HardwareViewModel(DashboardViewModel dashboard, GameLibraryViewModel library)
{
    public DashboardViewModel Dashboard { get; } = dashboard;
    public GameOptimizationPageViewModel Diagnostics { get; } = library.HardwareSection.Page;
    public GameLibraryViewModel Library { get; } = library;
}

public partial class HardwareView : UserControl
{
    public HardwareView(HardwareViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        DiagnosticsHost.DataContext = viewModel.Diagnostics;
        Loaded += async (_, _) =>
        {
            if (viewModel.Dashboard.ProcessorDetail.Length == 0)
                await viewModel.Dashboard.RefreshAsync();
            await viewModel.Library.InitializeAsync();
        };
    }

    /// <summary>Dentro de Ferramentas: o cabeçalho da página hospedeira substitui o próprio.</summary>
    public bool IsEmbedded
    {
        get => Header.Visibility != System.Windows.Visibility.Visible;
        set
        {
            Header.Visibility = value ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            Root.Margin = value ? new System.Windows.Thickness(28, 0, 28, 0) : new System.Windows.Thickness(28, 22, 28, 0);
        }
    }
}
