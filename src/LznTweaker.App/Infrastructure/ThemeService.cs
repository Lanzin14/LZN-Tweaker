using System.Windows;
using Microsoft.Win32;

namespace LznTweaker.Infrastructure;

/// <summary>Escolha de tema do usuário: seguir o Windows, claro ou escuro (Grafite + Azul).</summary>
public enum AppTheme
{
    System,
    Light,
    Dark
}

/// <summary>
/// Troca a paleta em tempo real: substitui Themes/Palette.Light.xaml ou Palette.Dark.xaml nos recursos do app (as telas
/// usam {DynamicResource Lzn.*}), atualiza os pincéis calculados em C# (<see cref="ThemeBrushes"/>) e avisa quem desenha
/// fora do WPF (barra de título). Em "Seguir o Windows", acompanha a troca de tema do sistema enquanto o app está aberto.
/// </summary>
public static class ThemeService
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private static bool _listening;

    public static AppTheme Choice { get; private set; } = AppTheme.System;

    /// <summary>Tema efetivo agora (o de "Seguir o Windows" já resolvido).</summary>
    public static bool IsDark { get; private set; } = true;

    public static event Action? Changed;

    public static void Apply(AppTheme choice)
    {
        Choice = choice;
        IsDark = choice switch
        {
            AppTheme.Light => false,
            AppTheme.Dark => true,
            _ => !WindowsUsesLightTheme()
        };
        if (choice == AppTheme.System && !_listening)
        {
            SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
            _listening = true;
        }

        var resources = Application.Current?.Resources.MergedDictionaries;
        if (resources is null)
            return;
        var source = new Uri($"pack://application:,,,/LznTweaker;component/Themes/Palette.{(IsDark ? "Dark" : "Light")}.xaml", UriKind.Absolute);
        var current = resources.FirstOrDefault(dictionary => dictionary.Source?.OriginalString.Contains("Palette.", StringComparison.Ordinal) == true);
        if (current?.Source?.OriginalString.EndsWith(IsDark ? "Palette.Dark.xaml" : "Palette.Light.xaml", StringComparison.Ordinal) != true)
        {
            var palette = new ResourceDictionary { Source = source };
            if (current is null)
                resources.Insert(0, palette);
            else
                resources[resources.IndexOf(current)] = palette;
        }
        ThemeBrushes.Refresh();
        Changed?.Invoke();
    }

    /// <summary>Lê a escolha "Modo de app" do Windows (Configurações › Personalização › Cores). Sem a chave, escuro.</summary>
    public static bool WindowsUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int value && value == 1;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            return false;
        }
    }

    private static void OnSystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (Choice != AppTheme.System || e.Category != UserPreferenceCategory.General)
            return;
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (Choice == AppTheme.System && WindowsUsesLightTheme() == IsDark)
                Apply(AppTheme.System);
        });
    }
}
