using System.Text.RegularExpressions;

namespace LznTweaker.Core.Debloat;

public enum AppxCategory
{
    /// <summary>Apps promocionais/pré-instalados da lista curada; seguros para remover.</summary>
    Sponsored,
    /// <summary>Apps da Microsoft que fazem parte da experiência do Windows; remover pode afetar funções.</summary>
    System,
    /// <summary>Apps de terceiros que não estão na lista curada; provavelmente instalados pelo usuário.</summary>
    UserInstalled
}

public sealed record AppxCatalogEntry(string FriendlyName, AppxCategory Category, string? Note = null);

/// <summary>
/// Classificação curada dos pacotes Appx. A lista de patrocinados se baseia no preset AppxDefault do WinUtil (MIT)
/// e em apps promocionais conhecidos; nada que não esteja aqui recebe o selo de "seguro para remover".
/// </summary>
public static partial class AppxCatalog
{
    private const string GamingNote = "Usado por recursos de jogos do Windows (Game Bar, gravação, Game Pass). Remova só se você não usa esses recursos.";
    private const string CodecNote = "Extensão de codec: sem ela, Fotos, Filmes e outros apps deixam de abrir esse formato de imagem/vídeo.";

    private static readonly Dictionary<string, AppxCatalogEntry> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Patrocinados / promocionais (preset AppxDefault do WinUtil + apps de terceiros pré-instalados) ──
        ["Microsoft.BingNews"] = new("Notícias (Microsoft News)", AppxCategory.Sponsored),
        ["Microsoft.BingWeather"] = new("Clima (MSN)", AppxCategory.Sponsored),
        ["Microsoft.BingSearch"] = new("Pesquisa Bing", AppxCategory.Sponsored),
        ["Microsoft.BingFinance"] = new("Finanças (MSN)", AppxCategory.Sponsored),
        ["Microsoft.BingSports"] = new("Esportes (MSN)", AppxCategory.Sponsored),
        ["Microsoft.GetHelp"] = new("Obter Ajuda", AppxCategory.Sponsored),
        ["Microsoft.Getstarted"] = new("Dicas", AppxCategory.Sponsored),
        ["Microsoft.WindowsFeedbackHub"] = new("Hub de Comentários", AppxCategory.Sponsored),
        ["Microsoft.MicrosoftOfficeHub"] = new("Microsoft 365 (Office)", AppxCategory.Sponsored),
        ["Microsoft.MicrosoftSolitaireCollection"] = new("Microsoft Solitaire Collection", AppxCategory.Sponsored),
        ["Microsoft.PowerAutomateDesktop"] = new("Power Automate", AppxCategory.Sponsored),
        ["Microsoft.Windows.DevHome"] = new("Dev Home", AppxCategory.Sponsored),
        ["Microsoft.StartExperiencesApp"] = new("Experiências do Iniciar (recomendações)", AppxCategory.Sponsored),
        ["Microsoft.Todos"] = new("Microsoft To Do", AppxCategory.Sponsored),
        ["Microsoft.Copilot"] = new("Copilot", AppxCategory.Sponsored),
        ["Clipchamp.Clipchamp"] = new("Clipchamp", AppxCategory.Sponsored),
        ["MicrosoftCorporationII.QuickAssist"] = new("Assistência Rápida", AppxCategory.Sponsored, "Necessário apenas para receber ou dar suporte remoto."),
        ["Microsoft.XboxApp"] = new("Xbox Console Companion (descontinuado)", AppxCategory.Sponsored),
        ["Microsoft.SkypeApp"] = new("Skype", AppxCategory.Sponsored),
        ["Microsoft.People"] = new("Pessoas", AppxCategory.Sponsored),
        ["Microsoft.WindowsMaps"] = new("Mapas", AppxCategory.Sponsored),
        ["Microsoft.Messaging"] = new("Mensagens", AppxCategory.Sponsored),
        ["Microsoft.OneConnect"] = new("Planos Pagos de Celular", AppxCategory.Sponsored),
        ["Microsoft.MixedReality.Portal"] = new("Portal de Realidade Misturada", AppxCategory.Sponsored),
        ["Microsoft.Microsoft3DViewer"] = new("Visualizador 3D", AppxCategory.Sponsored),
        ["Microsoft.Print3D"] = new("Print 3D", AppxCategory.Sponsored),
        ["Microsoft.3DBuilder"] = new("3D Builder", AppxCategory.Sponsored),
        ["Microsoft.MSPaint"] = new("Paint 3D", AppxCategory.Sponsored),
        ["Microsoft.Wallet"] = new("Carteira", AppxCategory.Sponsored),
        ["king.com.CandyCrushSaga"] = new("Candy Crush Saga", AppxCategory.Sponsored),
        ["king.com.CandyCrushSodaSaga"] = new("Candy Crush Soda Saga", AppxCategory.Sponsored),
        ["king.com.CandyCrushFriends"] = new("Candy Crush Friends", AppxCategory.Sponsored),
        ["king.com.BubbleWitch3Saga"] = new("Bubble Witch 3 Saga", AppxCategory.Sponsored),
        ["king.com.FarmHeroesSaga"] = new("Farm Heroes Saga", AppxCategory.Sponsored),
        ["BytedancePte.Ltd.TikTok"] = new("TikTok", AppxCategory.Sponsored),
        ["4DF9E0F8.Netflix"] = new("Netflix", AppxCategory.Sponsored),
        ["Disney.37853FC22B2CE"] = new("Disney+", AppxCategory.Sponsored),
        ["AmazonVideo.PrimeVideo"] = new("Prime Video", AppxCategory.Sponsored),
        ["Facebook.Facebook"] = new("Facebook", AppxCategory.Sponsored),
        ["Facebook.InstagramBeta"] = new("Instagram", AppxCategory.Sponsored),
        ["SpotifyAB.SpotifyMusic"] = new("Spotify", AppxCategory.UserInstalled, "Às vezes vem pré-instalado, mas muitas pessoas o instalam de propósito; por isso não é marcado como patrocinado."),

        // ── Apps do sistema com observações específicas ──
        ["Microsoft.WindowsCalculator"] = new("Calculadora", AppxCategory.System),
        ["Microsoft.WindowsCamera"] = new("Câmera", AppxCategory.System, "Apps que abrem a câmera pelo Windows deixam de ter o app padrão."),
        ["Microsoft.Paint"] = new("Paint", AppxCategory.System),
        ["Microsoft.Windows.Photos"] = new("Fotos", AppxCategory.System, "É o visualizador padrão de imagens; sem ele, imagens abrem em outro app ou pedem escolha."),
        ["Microsoft.WindowsNotepad"] = new("Bloco de Notas", AppxCategory.System, "É o editor padrão de .txt."),
        ["Microsoft.WindowsTerminal"] = new("Terminal", AppxCategory.System, "Terminal padrão do Windows 11; o PowerShell e o Prompt continuam disponíveis."),
        ["Microsoft.ScreenSketch"] = new("Ferramenta de Captura", AppxCategory.System, "Remove o atalho Win+Shift+S."),
        ["Microsoft.WindowsAlarms"] = new("Alarmes e Relógio", AppxCategory.System),
        ["Microsoft.WindowsSoundRecorder"] = new("Gravador de Som", AppxCategory.System),
        ["Microsoft.MicrosoftStickyNotes"] = new("Notas Autoadesivas", AppxCategory.System),
        ["Microsoft.ZuneMusic"] = new("Media Player", AppxCategory.System, "É o player padrão de músicas e vídeos do Windows 11."),
        ["Microsoft.ZuneVideo"] = new("Filmes e TV", AppxCategory.System),
        ["Microsoft.YourPhone"] = new("Vincular ao Celular", AppxCategory.System),
        ["MicrosoftWindows.CrossDevice"] = new("Experiência entre dispositivos", AppxCategory.System, "Usado pelo Vincular ao Celular e pela integração com o celular no Explorador."),
        ["Microsoft.OutlookForWindows"] = new("Outlook (novo)", AppxCategory.System),
        ["Microsoft.WindowsCommunicationsApps"] = new("Email e Calendário", AppxCategory.System),
        ["MicrosoftWindows.Client.WebExperience"] = new("Widgets", AppxCategory.System, "Remove o painel de Widgets do Windows 11."),
        ["Microsoft.GamingApp"] = new("Xbox", AppxCategory.System, GamingNote),
        ["Microsoft.XboxGamingOverlay"] = new("Xbox Game Bar", AppxCategory.System, GamingNote),
        ["Microsoft.XboxGameOverlay"] = new("Xbox Game Overlay", AppxCategory.System, GamingNote),
        ["Microsoft.XboxSpeechToTextOverlay"] = new("Xbox Speech to Text", AppxCategory.System, GamingNote),
        ["Microsoft.Xbox.TCUI"] = new("Xbox TCUI", AppxCategory.System, GamingNote),
        ["Microsoft.HEVCVideoExtension"] = new("Extensão de Vídeo HEVC", AppxCategory.System, CodecNote),
        ["Microsoft.AV1VideoExtension"] = new("Extensão de Vídeo AV1", AppxCategory.System, CodecNote),
        ["Microsoft.VP9VideoExtensions"] = new("Extensão de Vídeo VP9", AppxCategory.System, CodecNote),
        ["Microsoft.MPEG2VideoExtension"] = new("Extensão de Vídeo MPEG-2", AppxCategory.System, CodecNote),
        ["Microsoft.AVCEncoderVideoExtension"] = new("Codificador de Vídeo AVC", AppxCategory.System, CodecNote),
        ["Microsoft.HEIFImageExtension"] = new("Extensão de Imagem HEIF", AppxCategory.System, CodecNote),
        ["Microsoft.WebpImageExtension"] = new("Extensão de Imagem WebP", AppxCategory.System, CodecNote),
        ["Microsoft.RawImageExtension"] = new("Extensão de Imagem RAW", AppxCategory.System, CodecNote),
        ["Microsoft.WebMediaExtensions"] = new("Extensões de Mídia da Web", AppxCategory.System, CodecNote)
    };

    // Nunca listados: removê-los quebra a Store, o winget, jogos, o idioma do Windows ou outros apps.
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.WindowsStore", "Microsoft.StorePurchaseApp", "Microsoft.DesktopAppInstaller", "Microsoft.Winget.Source",
        "Microsoft.XboxIdentityProvider", "Microsoft.GamingServices", "Microsoft.SecHealthUI", "Microsoft.Windows.ShellExperienceHost",
        "Microsoft.ApplicationCompatibilityEnhancements", "Microsoft.MicrosoftEdge", "Microsoft.MicrosoftEdge.Stable",
        "Microsoft.MicrosoftEdgeDevToolsClient", "Microsoft.WindowsAppRuntime", "Microsoft.Services.Store.Engagement"
    };

    [GeneratedRegex(@"^(Microsoft\.LanguageExperiencePack|Microsoft\.Ink\.Handwriting|Microsoft\.WindowsAppRuntime|MicrosoftCorporationII\.WinAppRuntime|Microsoft\.VCLibs|Microsoft\.NET\.Native|Microsoft\.UI\.Xaml|Microsoft\.Services\.Store)", RegexOptions.IgnoreCase)]
    private static partial Regex ProtectedPrefix();

    /// <summary>
    /// Pacotes que nunca são oferecidos para remoção: frameworks, recursos, pacotes do sistema/não removíveis,
    /// pacotes de identidade de programas Win32 (assinatura Developer) e a lista protegida.
    /// </summary>
    public static bool IsProtected(string name, string signatureKind, bool isFramework, bool isResourcePackage, bool nonRemovable)
    {
        if (isFramework || isResourcePackage || nonRemovable)
            return true;
        if (string.Equals(signatureKind, "System", StringComparison.OrdinalIgnoreCase))
            return true;
        if (ProtectedNames.Contains(name) || ProtectedPrefix().IsMatch(name))
            return true;
        // Assinatura Developer = identidade de um programa instalado (Edge, VS Code, Teams...): só a lista curada entra.
        return string.Equals(signatureKind, "Developer", StringComparison.OrdinalIgnoreCase) && !Known.ContainsKey(name);
    }

    public static AppxCatalogEntry Classify(string name, string publisher)
    {
        if (Known.TryGetValue(name, out var entry))
            return entry;
        var isMicrosoft = publisher.Contains("CN=Microsoft", StringComparison.OrdinalIgnoreCase);
        return new AppxCatalogEntry(Prettify(name), isMicrosoft ? AppxCategory.System : AppxCategory.UserInstalled);
    }

    /// <summary>"BethesdaSoftworks.WolfensteinTNO-PC" → "Wolfenstein TNO-PC".</summary>
    internal static string Prettify(string name)
    {
        var lastPart = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
        if (lastPart.Length < 3 || lastPart.All(char.IsDigit))
            lastPart = name;
        return CamelCaseBoundary().Replace(lastPart, " ");
    }

    [GeneratedRegex(@"(?<=[a-z])(?=[A-Z])")]
    private static partial Regex CamelCaseBoundary();
}
