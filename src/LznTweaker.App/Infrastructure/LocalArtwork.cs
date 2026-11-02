using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LznTweaker.Infrastructure;

/// <summary>
/// Ícones de jogos e plataformas lidos de arquivos que já existem no PC (cache do Steam e executáveis instalados).
/// Nada é baixado nem incluído no projeto; quando o arquivo não existe, devolve nulo e a interface usa um monograma.
/// </summary>
public static class LocalArtwork
{
    /// <summary>Ícone que o próprio Steam guarda em appcache\librarycache\&lt;appid&gt; (o .jpg pequeno com nome de hash).</summary>
    public static ImageSource? SteamAppIcon(string? steamRoot, string? appId)
    {
        if (steamRoot is null || appId is null)
            return null;
        try
        {
            var folder = Path.Combine(steamRoot, "appcache", "librarycache", appId);
            if (!Directory.Exists(folder))
                return null;
            var file = Directory.EnumerateFiles(folder, "*.jpg", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetFileNameWithoutExtension(path).Length == 40)
                .OrderBy(path => new FileInfo(path).Length)
                .FirstOrDefault();
            return file is null ? null : LoadBitmap(file);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Capa horizontal (header.jpg, 460×215) que o Steam guarda em appcache\librarycache\&lt;appid&gt;; só leitura.</summary>
    public static ImageSource? SteamHeader(string? steamRoot, string? appId)
    {
        if (steamRoot is null || appId is null)
            return null;
        try
        {
            var folder = Path.Combine(steamRoot, "appcache", "librarycache", appId);
            if (!Directory.Exists(folder))
                return null;
            var file = Directory.EnumerateFiles(folder, "header.jpg", SearchOption.AllDirectories).FirstOrDefault();
            return file is null ? null : LoadBitmap(file);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Ícone principal de um executável instalado (ex.: steam.exe, RiotClientServices.exe).</summary>
    public static ImageSource? ExecutableIcon(string? path)
    {
        if (path is null || !File.Exists(path))
            return null;
        var large = new IntPtr[1];
        try
        {
            if (ExtractIconEx(path, 0, large, null, 1) == 0 || large[0] == IntPtr.Zero)
                return null;
            var source = Imaging.CreateBitmapSourceFromHIcon(large[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            if (large[0] != IntPtr.Zero)
                DestroyIcon(large[0]);
        }
    }

    /// <summary>Caminho do Riot Client registrado em ProgramData\Riot Games\RiotClientInstalls.json, se existir.</summary>
    public static string? RiotClientPath()
    {
        try
        {
            var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Riot Games", "RiotClientInstalls.json");
            if (!File.Exists(file))
                return null;
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            return document.RootElement.TryGetProperty("rc_default", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Executável do Epic Games Launcher na pasta padrão de instalação, se existir.</summary>
    public static string? EpicLauncherPath()
    {
        foreach (var programFiles in new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles })
        {
            var folder = Path.Combine(Environment.GetFolderPath(programFiles), "Epic Games", "Launcher", "Portal", "Binaries");
            foreach (var platform in new[] { "Win64", "Win32" })
            {
                var path = Path.Combine(folder, platform, "EpicGamesLauncher.exe");
                if (File.Exists(path))
                    return path;
            }
        }
        return null;
    }

    private static BitmapImage LoadBitmap(string file)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(file);
        image.EndInit();
        image.Freeze();
        return image;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int iconIndex, IntPtr[]? largeIcons, IntPtr[]? smallIcons, uint icons);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);
}
