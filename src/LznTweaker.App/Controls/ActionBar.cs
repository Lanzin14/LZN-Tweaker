using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LznTweaker.Controls;

/// <summary>
/// Barra de ação fixa no rodapé das páginas com seleção: resumo à esquerda ("3 selecionados"), aviso curto ao lado e
/// os botões à direita (o conteúdo; no máximo um botão principal). Mesmo lugar e mesmo formato em todas as páginas.
/// Ctrl+Enter aciona o botão principal da barra visível (o mesmo clique: revisão, confirmação e ponto de restauração
/// continuam valendo). Visual em Themes/Controls.xaml.
/// </summary>
public sealed class ActionBar : ContentControl
{
    public static readonly DependencyProperty SummaryProperty =
        DependencyProperty.Register(nameof(Summary), typeof(string), typeof(ActionBar));

    public static readonly DependencyProperty HintProperty =
        DependencyProperty.Register(nameof(Hint), typeof(string), typeof(ActionBar));

    private Window? _window;

    static ActionBar() =>
        FocusableProperty.OverrideMetadata(typeof(ActionBar), new FrameworkPropertyMetadata(false));

    public ActionBar()
    {
        Loaded += (_, _) =>
        {
            _window = Window.GetWindow(this);
            if (_window is not null)
                _window.PreviewKeyDown += OnWindowKeyDown;
        };
        Unloaded += (_, _) =>
        {
            if (_window is not null)
                _window.PreviewKeyDown -= OnWindowKeyDown;
            _window = null;
        };
    }

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

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.Control || !IsVisible)
            return;
        var primary = FindPrimary(this);
        if (primary is null || !primary.IsEnabled || primary.Command is not { } command || !command.CanExecute(primary.CommandParameter))
            return;
        command.Execute(primary.CommandParameter);
        e.Handled = true;
    }

    /// <summary>O botão principal da barra: o de estilo Lzn.PrimaryButton ou, numa ação destrutiva, Lzn.DangerButton.</summary>
    private Button? FindPrimary(DependencyObject parent)
    {
        var primaryStyle = TryFindResource("Lzn.PrimaryButton");
        var dangerStyle = TryFindResource("Lzn.DangerButton");
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Button button && (ReferenceEquals(button.Style, primaryStyle) || ReferenceEquals(button.Style, dangerStyle)))
                return button;
            if (FindPrimary(child) is { } found)
                return found;
        }
        return null;
    }
}
