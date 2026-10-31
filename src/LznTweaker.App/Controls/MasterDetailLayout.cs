using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LznTweaker.Controls;

/// <summary>
/// Lista à esquerda (<see cref="Master"/>) e detalhe do item selecionado (<see cref="DetailTemplate"/>). Com largura a partir de
/// <see cref="DockedMinWidth"/>, o detalhe fica fixo ao lado; mais estreito, abre por cima da lista e fecha com Esc,
/// clique fora ou o botão fechar (<see cref="CloseCommand"/>, que deve limpar a seleção). Visual em Themes/Controls.xaml.
/// </summary>
public sealed class MasterDetailLayout : Control
{
    public static readonly DependencyProperty MasterProperty =
        DependencyProperty.Register(nameof(Master), typeof(object), typeof(MasterDetailLayout));

    /// <summary>Modelo do detalhe, aplicado ao item selecionado (gera uma cópia para o painel fixo e outra para o sobreposto).</summary>
    public static readonly DependencyProperty DetailTemplateProperty =
        DependencyProperty.Register(nameof(DetailTemplate), typeof(DataTemplate), typeof(MasterDetailLayout));

    public static readonly DependencyProperty DetailTemplateSelectorProperty =
        DependencyProperty.Register(nameof(DetailTemplateSelector), typeof(DataTemplateSelector), typeof(MasterDetailLayout));

    /// <summary>O item selecionado: o detalhe só aparece quando não é nulo.</summary>
    public static readonly DependencyProperty SelectedProperty =
        DependencyProperty.Register(nameof(Selected), typeof(object), typeof(MasterDetailLayout), new PropertyMetadata(null, (d, _) => ((MasterDetailLayout)d).UpdateState()));

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(MasterDetailLayout),
            new PropertyMetadata("Selecione um item da lista para ver os detalhes."));

    public static readonly DependencyProperty CloseCommandProperty =
        DependencyProperty.Register(nameof(CloseCommand), typeof(ICommand), typeof(MasterDetailLayout));

    public static readonly DependencyProperty DockedMinWidthProperty =
        DependencyProperty.Register(nameof(DockedMinWidth), typeof(double), typeof(MasterDetailLayout), new PropertyMetadata(960d));

    private static readonly DependencyPropertyKey IsDockedKey =
        DependencyProperty.RegisterReadOnly(nameof(IsDocked), typeof(bool), typeof(MasterDetailLayout), new PropertyMetadata(true));
    public static readonly DependencyProperty IsDockedProperty = IsDockedKey.DependencyProperty;

    private static readonly DependencyPropertyKey IsOverlayOpenKey =
        DependencyProperty.RegisterReadOnly(nameof(IsOverlayOpen), typeof(bool), typeof(MasterDetailLayout), new PropertyMetadata(false));
    public static readonly DependencyProperty IsOverlayOpenProperty = IsOverlayOpenKey.DependencyProperty;

    static MasterDetailLayout() => FocusableProperty.OverrideMetadata(typeof(MasterDetailLayout), new FrameworkPropertyMetadata(false));

    public MasterDetailLayout()
    {
        SizeChanged += (_, e) =>
        {
            SetValue(IsDockedKey, e.NewSize.Width >= DockedMinWidth);
            UpdateState();
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && IsOverlayOpen && CloseCommand?.CanExecute(null) == true)
            {
                CloseCommand.Execute(null);
                e.Handled = true;
            }
        };
    }

    public object? Master { get => GetValue(MasterProperty); set => SetValue(MasterProperty, value); }
    public DataTemplate? DetailTemplate { get => (DataTemplate?)GetValue(DetailTemplateProperty); set => SetValue(DetailTemplateProperty, value); }
    public DataTemplateSelector? DetailTemplateSelector { get => (DataTemplateSelector?)GetValue(DetailTemplateSelectorProperty); set => SetValue(DetailTemplateSelectorProperty, value); }
    public object? Selected { get => GetValue(SelectedProperty); set => SetValue(SelectedProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public ICommand? CloseCommand { get => (ICommand?)GetValue(CloseCommandProperty); set => SetValue(CloseCommandProperty, value); }
    public double DockedMinWidth { get => (double)GetValue(DockedMinWidthProperty); set => SetValue(DockedMinWidthProperty, value); }
    public bool IsDocked => (bool)GetValue(IsDockedProperty);
    public bool IsOverlayOpen => (bool)GetValue(IsOverlayOpenProperty);

    private void UpdateState() => SetValue(IsOverlayOpenKey, !IsDocked && Selected is not null);
}
