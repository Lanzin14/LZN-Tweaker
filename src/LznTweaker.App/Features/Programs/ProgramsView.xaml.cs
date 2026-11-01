using System.Windows.Controls;

namespace LznTweaker.Features.Programs;

public partial class ProgramsView : UserControl
{
    public ProgramsView(ProgramsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
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
