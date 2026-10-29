using System.Windows.Controls;

namespace LznTweaker.Features.Backups;

public partial class BackupsView : UserControl
{
    public BackupsView(BackupsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        // O histórico muda a partir de outras páginas; relê sempre que a página aparece.
        Loaded += async (_, _) => await viewModel.RefreshAsync();
    }
}
