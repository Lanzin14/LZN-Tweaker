using System.Windows.Controls;

namespace LznTweaker.Features.Gpu;

public partial class GpuView : UserControl
{
    public GpuView(GpuViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        // Só detecta (leitura) na primeira exibição; nada é aplicado ao abrir a página.
        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }
}
