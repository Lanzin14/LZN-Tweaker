using System.Windows;
using System.Windows.Controls;
using LznTweaker.Shell;

namespace LznTweaker.Controls;

/// <summary>Cabeçalho explicativo de página: frase curta + "Saiba mais" expansível (ver <see cref="CategoryGuides"/>).</summary>
public partial class CategoryInfo : UserControl
{
    public static readonly DependencyProperty GuideProperty =
        DependencyProperty.Register(nameof(Guide), typeof(CategoryGuide), typeof(CategoryInfo));

    public CategoryInfo() => InitializeComponent();

    public CategoryGuide? Guide
    {
        get => (CategoryGuide?)GetValue(GuideProperty);
        set => SetValue(GuideProperty, value);
    }
}
