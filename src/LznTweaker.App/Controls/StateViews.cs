using System.Windows;
using System.Windows.Controls;
using LznTweaker.Infrastructure;

namespace LznTweaker.Controls;

/// <summary>
/// Aviso de página (informação, atenção ou erro) com orientação e "Detalhes técnicos" recolhível. Some quando
/// <see cref="Problem"/> é nulo. O conteúdo do controle são as ações (ex.: "Tentar de novo"). Visual em Themes/Controls.xaml.
/// </summary>
public sealed class Banner : ContentControl
{
    public static readonly DependencyProperty ProblemProperty =
        DependencyProperty.Register(nameof(Problem), typeof(ProblemInfo), typeof(Banner));

    static Banner() => FocusableProperty.OverrideMetadata(typeof(Banner), new FrameworkPropertyMetadata(false));

    public ProblemInfo? Problem
    {
        get => (ProblemInfo?)GetValue(ProblemProperty);
        set => SetValue(ProblemProperty, value);
    }
}

/// <summary>Estado vazio: ícone, frase que explica por que não há nada e, opcionalmente, uma ação (o conteúdo).</summary>
public sealed class EmptyState : ContentControl
{
    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(EmptyState), new PropertyMetadata(""));

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(EmptyState), new PropertyMetadata(""));

    static EmptyState() => FocusableProperty.OverrideMetadata(typeof(EmptyState), new FrameworkPropertyMetadata(false));

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }
}

/// <summary>Carregamento: indicador e texto no lugar do conteúdo que ainda está sendo lido.</summary>
public sealed class LoadingState : Control
{
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(LoadingState), new PropertyMetadata("Lendo…"));

    static LoadingState() => FocusableProperty.OverrideMetadata(typeof(LoadingState), new FrameworkPropertyMetadata(false));

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }
}
