using System.Windows;
using System.Windows.Controls;

namespace LznTweaker.Controls;

/// <summary>
/// Barra de ação fixa no rodapé das páginas com seleção: resumo à esquerda ("3 selecionados"), aviso curto ao lado e
/// os botões à direita (o conteúdo; no máximo um botão principal). Mesmo lugar e mesmo formato em todas as páginas.
/// Visual em Themes/Controls.xaml.
/// </summary>
public sealed class ActionBar : ContentControl
{
    public static readonly DependencyProperty SummaryProperty =
        DependencyProperty.Register(nameof(Summary), typeof(string), typeof(ActionBar));

    public static readonly DependencyProperty HintProperty =
        DependencyProperty.Register(nameof(Hint), typeof(string), typeof(ActionBar));

    static ActionBar() =>
        FocusableProperty.OverrideMetadata(typeof(ActionBar), new FrameworkPropertyMetadata(false));

    /// <summary>Texto principal, em destaque (ex.: "3 selecionados").</summary>
    public string? Summary
    {
        get => (string?)GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    /// <summary>Aviso curto ao lado do resumo (ex.: "ponto de restauração antes de aplicar").</summary>
    public string? Hint
    {
        get => (string?)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }
}
