using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace LznTweaker.Features.GameProfiles;

public partial class GameOptimizationPageView : UserControl
{
    public GameOptimizationPageView() => InitializeComponent();
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public static InverseBoolToVisibilityConverter Instance { get; } = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
