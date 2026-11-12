using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using LznTweaker.Infrastructure;
using LznTweaker.Shell;

namespace LznTweaker;

public partial class MainWindow : Window
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmCaptionColor = 35;

    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += (_, _) => ApplyTitleBar();
        ThemeService.Changed += ApplyTitleBar;
        Closed += (_, _) => ThemeService.Changed -= ApplyTitleBar;
    }

    /// <summary>
    /// Barra de título nativa no tema do app: clara ou escura (Windows 10 20H1+) e, no Windows 11, na cor de fundo
    /// (Lzn.Background), para a janela parecer uma peça só.
    /// </summary>
    private void ApplyTitleBar()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
            return;
        var dark = ThemeService.IsDark ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref dark, sizeof(int));
        if (TryFindResource("Lzn.Background") is SolidColorBrush background)
        {
            var captionColor = background.Color.R | (background.Color.G << 8) | (background.Color.B << 16); // COLORREF 0x00BBGGRR
            _ = DwmSetWindowAttribute(handle, DwmCaptionColor, ref captionColor, sizeof(int));
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
