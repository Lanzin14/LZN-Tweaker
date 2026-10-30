using System.Windows.Controls;

namespace LznTweaker.Features.Programs;

public partial class ProgramsView : UserControl
{
    public ProgramsView(ProgramsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
