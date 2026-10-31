using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace LznTweaker.Features.TweakCards;

public partial class TweakCardsView : UserControl
{
    public TweakCardsView(TweakCardsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
        // Esc fecha o painel de detalhes.
        InputBindings.Add(new KeyBinding(viewModel.CloseDetailsCommand, Key.Escape, ModifierKeys.None));
        // Lista + detalhe fixo só quando cabem os dois; mais estreito, o detalhe abre por cima da lista.
        SizeChanged += (_, e) => viewModel.IsDetailDocked = viewModel.UsesListLayout && e.NewSize.Width >= DockedDetailMinWidth;
    }

    /// <summary>Largura mínima da página para a lista (360 px) e o detalhe (420 px) ficarem lado a lado, com margens.</summary>
    public const double DockedDetailMinWidth = 960;
}

public sealed class GeneralPageView(GeneralPageViewModel viewModel) : TweakCardsView(viewModel);
public sealed class NetworkPageView(NetworkPageViewModel viewModel) : TweakCardsView(viewModel);
public sealed class FixesPageView(FixesPageViewModel viewModel) : TweakCardsView(viewModel);

public sealed class DetailsTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Tweak { get; set; }
    public DataTemplate? Action { get; set; }
    public DataTemplate? Guide { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) => item switch
    {
        TweakCardViewModel => Tweak,
        QuickActionCardViewModel => Action,
        GuideCardViewModel => Guide,
        _ => null
    };
}

/// <summary>Linha do layout Foco: guias (Discord) têm modelo próprio; ajustes e ferramentas usam a linha limpa.</summary>
public sealed class FocusRowSelector : DataTemplateSelector
{
    public DataTemplate? Card { get; set; }
    public DataTemplate? Guide { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) => item is GuideCardViewModel ? Guide : Card;
}

public sealed class InverseBooleanConverter : IValueConverter
{
    public static InverseBooleanConverter Instance { get; } = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
}
