using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LznTweaker.Infrastructure;

/// <summary>Conversores de visibilidade usados pelas páginas novas (acessados via x:Static ...Instance).</summary>
public sealed class VisibilityConverter : IValueConverter
{
    private readonly Func<object?, bool> _visible;

    private VisibilityConverter(Func<object?, bool> visible) => _visible = visible;

    public static VisibilityConverter WhenTrue { get; } = new(value => value is true);
    public static VisibilityConverter WhenFalse { get; } = new(value => value is not true);
    public static VisibilityConverter WhenNotNull { get; } = new(value => value is not null && value is not string { Length: 0 });
    public static VisibilityConverter WhenNull { get; } = new(value => value is null || value is string { Length: 0 });
    public static VisibilityConverter WhenZero { get; } = new(value => value is 0);
    public static VisibilityConverter WhenNonZero { get; } = new(value => value is int number && number != 0);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        _visible(value) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
