using System.Windows.Controls;
using LznTweaker.Core.GameOptimization;
using LznTweaker.Features.GameProfiles;
using LznTweaker.ViewModels;

namespace LznTweaker.Features.Hardware;

public sealed class HardwareViewModel(DashboardViewModel dashboard, GameLibraryViewModel library)
{
    public DashboardViewModel Dashboard { get; } = dashboard;
    public GameOptimizationPageViewModel Diagnostics { get; } = library.Sections.First(section => section.Page.Area == OptimizationArea.Hardware).Page;
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
}
