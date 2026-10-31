using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace LznTweaker.UiSmoke;

/// <summary>
/// Regras do sistema de design (docs/REDESIGN-OBSIDIAN.md) conferidas no código-fonte: cores só nos tokens e
/// contraste mínimo de texto (WCAG AA, 4,5:1) nos pares que as telas usam.
/// </summary>
public sealed partial class DesignSystemGuardTests
{
    private static string AppFolder([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "src", "LznTweaker.App"));

    private static string TokensPath => Path.Combine(AppFolder(), "Themes", "Tokens.xaml");

    [GeneratedRegex("""(?<![&\w])#(?:[0-9A-Fa-f]{8}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{3,4})\b""")]
    private static partial Regex HexColor();

    [GeneratedRegex("""<Color x:Key="Lzn\.Color\.(\w+)">#([0-9A-Fa-f]{6})</Color>""")]
    private static partial Regex TokenColor();

    [Fact]
    public void Hex_colors_live_only_in_the_tokens()
    {
        var offenders = Directory.EnumerateFiles(AppFolder(), "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                           !string.Equals(path, TokensPath, StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (path, line, index)))
            .Where(item => HexColor().IsMatch(item.line))
            .Select(item => $"{Path.GetRelativePath(AppFolder(), item.path)}:{item.index + 1}: {item.line.Trim()}")
            .ToList();
        Assert.True(offenders.Count == 0, "Cor em hexadecimal fora de Themes/Tokens.xaml:\n" + string.Join("\n", offenders));
    }

    // Pares texto/fundo em uso (primitivos de Tokens.xaml). Muted sobre Elevated fica de fora de propósito:
    // a regra é não usar Muted para texto sobre Elevated (fica em ~4:1).
    [Theory]
    [InlineData("Gray50", "Gray950", "Texto sobre o fundo")]
    [InlineData("Gray50", "Gray900", "Texto sobre Surface")]
    [InlineData("Gray50", "Gray800", "Texto sobre Elevated")]
    [InlineData("Gray300", "Gray800", "Texto secundário sobre Elevated")]
    [InlineData("Gray400", "Gray950", "Muted sobre o fundo")]
    [InlineData("Gray400", "Gray900", "Muted sobre Surface")]
    [InlineData("Gray950", "Violet500", "Texto do botão principal")]
    [InlineData("Gray950", "Violet400", "Texto do botão principal no hover")]
    [InlineData("Violet500", "Gray950", "Roxo como texto sobre o fundo")]
    [InlineData("Violet400", "Gray900", "Links e Saiba mais sobre Surface")]
    [InlineData("Violet200", "VioletSoft", "Texto de selo roxo")]
    [InlineData("Green300", "GreenSoft", "Selo de sucesso")]
    [InlineData("Amber300", "AmberSoft", "Selo de atenção")]
    [InlineData("Red400", "RedSoft", "Selo de erro")]
    [InlineData("Green300", "Gray900", "Estado ativo sobre Surface")]
    [InlineData("Amber300", "Gray900", "Estado pendente sobre Surface")]
    [InlineData("Red400", "Gray900", "Erro sobre Surface")]
    [InlineData("Gray50", "Indigo", "Texto da sidebar")]
    [InlineData("IndigoMuted", "Indigo", "Texto secundário da sidebar")]
    [InlineData("Gray50", "IndigoHover", "Item da sidebar no hover")]
    [InlineData("Gray50", "IndigoActive", "Item ativo da sidebar")]
    [InlineData("Amber300", "IndigoActive", "Aviso no rodapé da sidebar")]
    public void Text_pairs_meet_wcag_aa(string foreground, string background, string usage)
    {
        var colors = TokenColor().Matches(File.ReadAllText(TokensPath)).ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value);
        var ratio = Contrast(colors[foreground], colors[background]);
        Assert.True(ratio >= 4.5, $"{usage}: {foreground} sobre {background} tem {ratio:0.00}:1 (mínimo 4,5:1).");
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
