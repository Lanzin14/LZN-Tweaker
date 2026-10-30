using System.Windows;
using System.Windows.Media;

namespace LznTweaker.Infrastructure;

/// <summary>
/// Pincéis de status lidos do tema (Themes/Theme.xaml) para uso nos ViewModels. Assim a paleta tem um só lugar:
/// mudar uma cor no tema muda também os indicadores calculados em C#.
/// </summary>
public static class ThemeBrushes
{
    public static SolidColorBrush Success => Get("Lzn.RiskLow");
    public static SolidColorBrush Warning => Get("Lzn.RiskWarn");
    public static SolidColorBrush Danger => Get("Lzn.RiskHigh");
    public static SolidColorBrush Accent => Get("Lzn.AccentHover");
    public static SolidColorBrush AccentText => Get("Lzn.AccentText");
    public static SolidColorBrush Muted => Get("Lzn.TextMuted");

    public static SolidColorBrush SuccessSoft => Get("Lzn.SuccessSoft");
    public static SolidColorBrush SuccessStroke => Get("Lzn.SuccessStroke");
    public static SolidColorBrush WarningSoft => Get("Lzn.WarnSoft");
    public static SolidColorBrush WarningStroke => Get("Lzn.WarnStroke");
    public static SolidColorBrush DangerSoft => Get("Lzn.DangerSoft");
    public static SolidColorBrush DangerStroke => Get("Lzn.DangerStroke");
    public static SolidColorBrush AccentSoft => Get("Lzn.AccentSoft");
    public static SolidColorBrush AccentStroke => Get("Lzn.AccentStroke");

    // Sem Application (ex.: ferramentas fora do app), devolve cinza em vez de falhar.
    private static SolidColorBrush Get(string key) =>
        Application.Current?.TryFindResource(key) as SolidColorBrush ?? Fallback;

    private static readonly SolidColorBrush Fallback = CreateFallback();

    private static SolidColorBrush CreateFallback()
    {
        var brush = new SolidColorBrush(Colors.Gray);
        brush.Freeze();
        return brush;
    }
}
