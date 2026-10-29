using System.Windows;
using System.Windows.Controls;

namespace LznTweaker.Controls;

/// <summary>
/// Grade responsiva: calcula quantas colunas cabem (largura mínima por item) e estica os itens para ocupar a linha.
/// Em janelas largas mostra 3 cards por linha; em janelas estreitas cai para 2 ou 1, sem encolher o texto.
/// Todos os itens de uma linha recebem a mesma altura.
/// </summary>
public sealed class AdaptiveGridPanel : Panel
{
    public static readonly DependencyProperty MinItemWidthProperty = DependencyProperty.Register(
        nameof(MinItemWidth), typeof(double), typeof(AdaptiveGridPanel),
        new FrameworkPropertyMetadata(270d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty MaxColumnsProperty = DependencyProperty.Register(
        nameof(MaxColumns), typeof(int), typeof(AdaptiveGridPanel),
        new FrameworkPropertyMetadata(3, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(AdaptiveGridPanel),
        new FrameworkPropertyMetadata(12d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double MinItemWidth { get => (double)GetValue(MinItemWidthProperty); set => SetValue(MinItemWidthProperty, value); }
    public int MaxColumns { get => (int)GetValue(MaxColumnsProperty); set => SetValue(MaxColumnsProperty, value); }
    public double Spacing { get => (double)GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    private int _columns = 1;
    private readonly List<double> _rowHeights = [];

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? MinItemWidth * MaxColumns + Spacing * (MaxColumns - 1) : availableSize.Width;
        _columns = Math.Clamp((int)((width + Spacing) / (MinItemWidth + Spacing)), 1, Math.Max(1, MaxColumns));
        var itemWidth = Math.Max(0, (width - Spacing * (_columns - 1)) / _columns);

        _rowHeights.Clear();
        var visible = InternalChildren.Cast<UIElement>().Where(child => child.Visibility != Visibility.Collapsed).ToArray();
        for (var index = 0; index < visible.Length; index++)
        {
            visible[index].Measure(new Size(itemWidth, double.PositiveInfinity));
            var row = index / _columns;
            if (row == _rowHeights.Count)
                _rowHeights.Add(0);
            _rowHeights[row] = Math.Max(_rowHeights[row], visible[index].DesiredSize.Height);
        }
        foreach (var child in InternalChildren.Cast<UIElement>().Where(child => child.Visibility == Visibility.Collapsed))
            child.Measure(new Size(0, 0));

        var height = _rowHeights.Sum() + Spacing * Math.Max(0, _rowHeights.Count - 1);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var itemWidth = Math.Max(0, (finalSize.Width - Spacing * (_columns - 1)) / _columns);
        var visible = InternalChildren.Cast<UIElement>().Where(child => child.Visibility != Visibility.Collapsed).ToArray();
        double y = 0;
        for (var index = 0; index < visible.Length; index++)
        {
            var row = index / _columns;
            var column = index % _columns;
            if (column == 0 && row > 0)
                y += _rowHeights[row - 1] + Spacing;
            var height = row < _rowHeights.Count ? _rowHeights[row] : visible[index].DesiredSize.Height;
            visible[index].Arrange(new Rect(column * (itemWidth + Spacing), y, itemWidth, height));
        }
        return finalSize;
    }
}
