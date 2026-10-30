using System.Windows;
using System.Windows.Controls;
using LznTweaker.Shell;

namespace LznTweaker.Controls;

/// <summary>
/// Cabeçalho padrão das páginas: título (igual ao nome no menu), descrição com "Saiba mais", linha de status e, à
/// direita, as ações da página (o conteúdo do controle; no máximo uma ação principal). Visual em Themes/Controls.xaml.
/// </summary>
public sealed class PageHeader : ContentControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(PageHeader), new PropertyMetadata(""));

    public static readonly DependencyProperty GuideProperty =
        DependencyProperty.Register(nameof(Guide), typeof(CategoryGuide), typeof(PageHeader));

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(string), typeof(PageHeader));

    static PageHeader() =>
        FocusableProperty.OverrideMetadata(typeof(PageHeader), new FrameworkPropertyMetadata(false));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public CategoryGuide? Guide
    {
        get => (CategoryGuide?)GetValue(GuideProperty);
        set => SetValue(GuideProperty, value);
    }

    /// <summary>Linha curta de estado (ex.: "14 de 33 ajustes ativos · lido às 14:52"); some quando vazia.</summary>
    public string? Status
    {
        get => (string?)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }
}
