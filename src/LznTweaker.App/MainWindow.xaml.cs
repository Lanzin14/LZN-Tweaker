using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
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
        SourceInitialized += (_, _) => ApplyDarkTitleBar();
    }

    /// <summary>Barra de título nativa escura (Windows 10 20H1+); no Windows 11 também na cor de fundo do app.</summary>
    private void ApplyDarkTitleBar()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var enabled = 1;
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
        var captionColor = 0x000E0A0A; // COLORREF (0x00BBGGRR) de #0A0A0E, o fundo do app
        _ = DwmSetWindowAttribute(handle, DwmCaptionColor, ref captionColor, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
