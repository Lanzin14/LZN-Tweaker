using System.Windows.Controls;
using LznTweaker.ViewModels;

namespace LznTweaker.Views;

/// <summary>Biblioteca de Tweaks (Advanced). Toda a lógica e as confirmações ficam no <see cref="TweaksViewModel"/>.</summary>
public partial class TweaksView : UserControl
{
    public TweaksView(TweaksViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.RefreshAsync();
    }
}
