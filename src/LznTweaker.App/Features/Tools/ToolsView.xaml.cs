using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using LznTweaker.Features.Hardware;
using LznTweaker.Features.Programs;
using LznTweaker.Features.TweakCards;

namespace LznTweaker.Features.Tools;

public partial class ToolsView : UserControl
{
    public ToolsView(ToolsViewModel viewModel, FixesPageViewModel fixes, NetworkPageViewModel network, HardwareView hardware, ProgramsView programs)
    {
        InitializeComponent();
        DataContext = viewModel;
        hardware.IsEmbedded = true;
        programs.IsEmbedded = true;
        HardwareHost.Content = hardware;
        ProgramsHost.Content = programs;
        // Estado e "Executado às…" das ferramentas vêm das páginas de cards; lê na primeira abertura.
        Loaded += async (_, _) =>
        {
            await fixes.InitializeAsync();
            await network.InitializeAsync();
        };
    }
}

/// <summary>Aba marcada quando a chave dela é a selecionada.</summary>
public sealed class KeyEqualsConverter : IMultiValueConverter
{
    public static KeyEqualsConverter Instance { get; } = new();
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length == 2 && values[0] is string a && values[1] is string b && a == b;
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
