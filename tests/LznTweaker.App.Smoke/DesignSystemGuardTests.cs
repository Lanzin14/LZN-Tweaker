using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace LznTweaker.UiSmoke;

/// <summary>
/// Regras do sistema de design conferidas no código-fonte: cores só nas paletas (Themes/Palette.*.xaml), as duas com
/// as mesmas chaves, e contraste mínimo (WCAG AA) nos pares que as telas usam, no tema claro e no escuro.
/// </summary>
public sealed partial class DesignSystemGuardTests
{
    private static string AppFolder([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "src", "LznTweaker.App"));

    private static string ThemesFolder => Path.Combine(AppFolder(), "Themes");
    private static string PalettePath(string theme) => Path.Combine(ThemesFolder, $"Palette.{theme}.xaml");

    [GeneratedRegex("""(?<![&\w])#(?:[0-9A-Fa-f]{8}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{3,4})\b""")]
    private static partial Regex HexColor();

    [GeneratedRegex("""<Color x:Key="Lzn\.Color\.(\w+)">#(?:[0-9A-Fa-f]{2})?([0-9A-Fa-f]{6})</Color>""")]
    private static partial Regex PaletteColor();

    [GeneratedRegex("<SolidColorBrush x:Key=\"([^\"]+)\"")]
    private static partial Regex PaletteBrush();

    [Fact]
    public void Hex_colors_live_only_in_the_palettes()
    {
        var offenders = Directory.EnumerateFiles(AppFolder(), "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                           !Path.GetFileName(path).StartsWith("Palette.", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (path, line, index)))
            .Where(item => HexColor().IsMatch(item.line))
            .Select(item => $"{Path.GetRelativePath(AppFolder(), item.path)}:{item.index + 1}: {item.line.Trim()}")
            .ToList();
        Assert.True(offenders.Count == 0, "Cor em hexadecimal fora de Themes/Palette.*.xaml:\n" + string.Join("\n", offenders));
    }

    // Os dois temas precisam das mesmas chaves: a troca em tempo real não pode deixar um pincel sem valor.
    [Fact]
    public void Light_and_dark_palettes_define_the_same_keys()
    {
        static string[] Keys(string theme) => PaletteBrush().Matches(File.ReadAllText(PalettePath(theme)))
            .Select(match => match.Groups[1].Value).Order().ToArray();
        Assert.Equal(Keys("Light"), Keys("Dark"));
    }

    // Pares texto/fundo em uso, nos dois temas (os mesmos que tools/theme/paletas.py confere ao gerar).
    [Theory]
    [InlineData("Light", "Text", "Background", "Texto sobre o fundo")]
    [InlineData("Light", "Text", "Surface", "Texto sobre superfície")]
    [InlineData("Light", "Text", "Active", "Texto sobre item ativo")]
    [InlineData("Light", "TextSecondary", "Raised", "Secundário sobre linha selecionada")]
    [InlineData("Light", "TextMuted", "Background", "Apagado sobre o fundo")]
    [InlineData("Light", "TextMuted", "Surface", "Apagado sobre superfície")]
    [InlineData("Light", "TextMuted", "Raised", "Apagado sobre linha selecionada")]
    [InlineData("Light", "OnAccent", "Accent", "Texto do botão principal")]
    [InlineData("Light", "OnAccent", "AccentStrong", "Botão principal no hover")]
    [InlineData("Light", "AccentHover", "Surface", "Links sobre superfície")]
    [InlineData("Light", "AccentHover", "Background", "Links sobre o fundo")]
    [InlineData("Light", "AccentText", "AccentSoft", "Texto de selo de destaque")]
    [InlineData("Light", "Success", "Surface", "Estado ativo")]
    [InlineData("Light", "Warning", "Surface", "Estado pendente")]
    [InlineData("Light", "Danger", "Surface", "Erro")]
    [InlineData("Light", "Success", "SuccessSoft", "Selo de sucesso")]
    [InlineData("Light", "Warning", "WarningSoft", "Selo de atenção")]
    [InlineData("Light", "Danger", "DangerSoft", "Selo de erro")]
    [InlineData("Light", "Info", "Surface", "Em andamento")]
    [InlineData("Light", "Text", "Sidebar", "Texto da sidebar")]
    [InlineData("Light", "SidebarMuted", "Sidebar", "Secundário da sidebar")]
    [InlineData("Light", "Text", "SidebarActive", "Item ativo da sidebar")]
    [InlineData("Light", "Warning", "Sidebar", "Aviso no rodapé da sidebar")]
    [InlineData("Dark", "Text", "Background", "Texto sobre o fundo")]
    [InlineData("Dark", "Text", "Surface", "Texto sobre superfície")]
    [InlineData("Dark", "Text", "Active", "Texto sobre item ativo")]
    [InlineData("Dark", "TextSecondary", "Raised", "Secundário sobre linha selecionada")]
    [InlineData("Dark", "TextMuted", "Background", "Apagado sobre o fundo")]
    [InlineData("Dark", "TextMuted", "Surface", "Apagado sobre superfície")]
    [InlineData("Dark", "TextMuted", "Raised", "Apagado sobre linha selecionada")]
    [InlineData("Dark", "OnAccent", "Accent", "Texto do botão principal")]
    [InlineData("Dark", "OnAccent", "AccentStrong", "Botão principal no hover")]
    [InlineData("Dark", "AccentHover", "Surface", "Links sobre superfície")]
    [InlineData("Dark", "AccentHover", "Background", "Links sobre o fundo")]
    [InlineData("Dark", "AccentText", "AccentSoft", "Texto de selo de destaque")]
    [InlineData("Dark", "Success", "Surface", "Estado ativo")]
    [InlineData("Dark", "Warning", "Surface", "Estado pendente")]
    [InlineData("Dark", "Danger", "Surface", "Erro")]
    [InlineData("Dark", "Success", "SuccessSoft", "Selo de sucesso")]
    [InlineData("Dark", "Warning", "WarningSoft", "Selo de atenção")]
    [InlineData("Dark", "Danger", "DangerSoft", "Selo de erro")]
    [InlineData("Dark", "Info", "Surface", "Em andamento")]
    [InlineData("Dark", "Text", "Sidebar", "Texto da sidebar")]
    [InlineData("Dark", "SidebarMuted", "Sidebar", "Secundário da sidebar")]
    [InlineData("Dark", "Text", "SidebarActive", "Item ativo da sidebar")]
    [InlineData("Dark", "Warning", "Sidebar", "Aviso no rodapé da sidebar")]
    public void Text_pairs_meet_wcag_aa(string theme, string foreground, string background, string usage) =>
        AssertContrast(theme, foreground, background, usage, 4.5);

    // Componentes de interface (WCAG 1.4.11): a borda de caixa, interruptor e busca precisa se destacar do fundo.
    [Theory]
    [InlineData("Light", "ControlStroke", "Background", "Borda de controle sobre o fundo")]
    [InlineData("Light", "ControlStroke", "Surface", "Borda de controle sobre superfície")]
    [InlineData("Light", "ControlStroke", "Raised", "Borda de controle sobre linha selecionada")]
    [InlineData("Light", "Accent", "Raised", "Interruptor ligado sobre linha selecionada")]
    [InlineData("Light", "Accent", "Surface", "Indicador de seleção")]
    [InlineData("Dark", "ControlStroke", "Background", "Borda de controle sobre o fundo")]
    [InlineData("Dark", "ControlStroke", "Surface", "Borda de controle sobre superfície")]
    [InlineData("Dark", "ControlStroke", "Raised", "Borda de controle sobre linha selecionada")]
    [InlineData("Dark", "Accent", "Raised", "Interruptor ligado sobre linha selecionada")]
    [InlineData("Dark", "Accent", "Surface", "Indicador de seleção")]
    public void Control_borders_meet_wcag_non_text_contrast(string theme, string foreground, string background, string usage) =>
        AssertContrast(theme, foreground, background, usage, 3.0);

    private static void AssertContrast(string theme, string foreground, string background, string usage, double minimum)
    {
        var colors = PaletteColor().Matches(File.ReadAllText(PalettePath(theme))).ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value);
        var ratio = Contrast(colors[foreground], colors[background]);
        Assert.True(ratio >= minimum, $"{theme} · {usage}: {foreground} sobre {background} tem {ratio:0.00}:1 (mínimo {minimum:0.0}:1).");
    }

    private static double Contrast(string a, string b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(string hex)
    {
        static double Channel(string hex, int offset)
        {
            var value = Convert.ToInt32(hex.Substring(offset, 2), 16) / 255.0;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(hex, 0) + 0.7152 * Channel(hex, 2) + 0.0722 * Channel(hex, 4);
    }
}
