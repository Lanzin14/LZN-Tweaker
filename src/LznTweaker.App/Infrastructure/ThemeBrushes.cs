using System.Windows;
using System.Windows.Media;

namespace LznTweaker.Infrastructure;

/// <summary>
/// Pincéis de status lidos do tema (Themes/Palette.*.xaml) para uso nos ViewModels. Assim a paleta tem um só lugar:
/// mudar uma cor no tema muda também os indicadores calculados em C#. Cada chave devolve sempre o mesmo pincel; ao trocar
/// de tema, <see cref="Refresh"/> muda a cor dele, e tudo o que já estava ligado a ele acompanha sem recarregar a tela.
/// </summary>
public static class ThemeBrushes
{
    private static readonly Dictionary<string, SolidColorBrush> Cache = [];

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

    /// <summary>Copia a cor atual do tema para cada pincel já entregue (chamado pelo <see cref="ThemeService"/>).</summary>
    public static void Refresh()
    {
        foreach (var (key, brush) in Cache)
            brush.Color = Lookup(key);
    }

    private static SolidColorBrush Get(string key)
    {
        // O pincel não é congelado (a cor muda com o tema), então pertence à thread da interface: cria sempre lá.
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
            return dispatcher.Invoke(() => Get(key));
        if (!Cache.TryGetValue(key, out var brush))
        {
            brush = new SolidColorBrush(Lookup(key));
            Cache[key] = brush;
        }
        return brush;
    }

    // Sem Application (ex.: ferramentas fora do app), devolve cinza em vez de falhar.
    private static Color Lookup(string key) =>
        (Application.Current?.TryFindResource(key) as SolidColorBrush)?.Color ?? Colors.Gray;
}
