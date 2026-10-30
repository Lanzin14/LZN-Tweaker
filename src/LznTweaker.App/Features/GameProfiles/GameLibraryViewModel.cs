using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LznTweaker.Core.Changes;
using LznTweaker.Core.GameOptimization;
using LznTweaker.Core.Startup;
using LznTweaker.Infrastructure;

namespace LznTweaker.Features.GameProfiles;

public enum LibraryPlatform { Windows, Steam, Riot }

/// <summary>Ícone de plataforma ao lado do jogo. Sem arquivo local, mostra só a inicial (Windows usa o logotipo vetorial).</summary>
public sealed record PlatformBadge(LibraryPlatform Platform, string Name, ImageSource? Icon)
{
    public bool IsWindows => Platform == LibraryPlatform.Windows;
    public bool HasIcon => Icon is not null;
    public bool ShowsInitial => !IsWindows && Icon is null;
    public string Initial => Name[..1];
}

/// <param name="monogram">Texto do ícone quando não há arte local (ex.: "CS2").</param>
/// <param name="glyph">Glifo do Segoe Fluent Icons para seções que não são jogos.</param>
public sealed partial class LibrarySection(string title, GameOptimizationPageViewModel page, LibraryPlatform[] platforms,
    SupportedGame? game = null, string? monogram = null, string? glyph = null, bool windowsLogo = false) : ObservableObject
{
    public string Title { get; } = title;
    public GameOptimizationPageViewModel Page { get; } = page;
    public SupportedGame? Game { get; } = game;
    public bool IsDeadlock => Game?.Id == SupportedGames.DeadlockId;
    public LibraryPlatform[] PlatformKinds { get; } = platforms;
    public string? Monogram { get; } = monogram;
    public string? Glyph { get; } = glyph;
    public bool ShowsWindowsLogo { get; } = windowsLogo;

    [ObservableProperty]
    private string _subtitle = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIcon), nameof(ShowsMonogram), nameof(ShowsGlyph))]
    private ImageSource? _icon;

    [ObservableProperty]
    private IReadOnlyList<PlatformBadge> _platforms = [];

    public bool HasIcon => Icon is not null;
    public bool ShowsMonogram => Icon is null && Monogram is not null;
    public bool ShowsGlyph => Icon is null && Monogram is null && Glyph is not null;
}

/// <summary>Biblioteca de otimização: Windows, Steam, cada jogo e hardware. Cada seção mantém a própria página e estado.</summary>
public sealed partial class GameLibraryViewModel : ObservableObject
{
    private readonly GameLocator _locator;
    private readonly GamingDiagnosticsService _diagnostics;
    private readonly StartupManagerService _startup;
    private bool _initialized;

    public GameLibraryViewModel(GameLocator locator, GamingDiagnosticsService diagnostics, StartupManagerService startup,
        ShaderCacheService shaderCache, SteamFolderBackupService steamFolders, RestorePointGuard restorePointGuard,
        RestorePointPrompt restorePointPrompt, IUserDialogs dialogs)
    {
        _locator = locator;
        _diagnostics = diagnostics;
        _startup = startup;
        GameOptimizationPageViewModel Page(OptimizationArea area, string title, string description) =>
            new(area, title, description, shaderCache, steamFolders, restorePointGuard, restorePointPrompt, dialogs);

        Sections =
        [
            new("Windows (todos os jogos)", Page(OptimizationArea.Windows, "Windows — vale para todos os jogos",
                "Itens do Windows que afetam todos os jogos: caches de shaders do DirectX e da placa de vídeo, energia, Game Bar e disco."),
                [LibraryPlatform.Windows], windowsLogo: true),
            new("Steam (compartilhado)", Page(OptimizationArea.Steam, "Steam — opções do cliente",
                "Opções do cliente Steam, como overlay, gravação, downloads e Steam Cloud. O cache de shaders de cada jogo fica na página do jogo."),
                [LibraryPlatform.Steam], glyph: "\uE7FC"),
            new(SupportedGames.Cs2.Name, Page(OptimizationArea.Cs2, "Counter-Strike 2",
                "Cache de shaders do CS2 na Steam, configurações recomendadas e verificação do jogo."),
                [LibraryPlatform.Windows, LibraryPlatform.Steam], SupportedGames.Cs2, "CS2"),
            new(SupportedGames.Valorant.Name, Page(OptimizationArea.Valorant, "VALORANT",
                "Só o que vale para o VALORANT. Nada do CS2 é reaproveitado e o anticheat Vanguard nunca é tocado."),
                [LibraryPlatform.Windows, LibraryPlatform.Riot], SupportedGames.Valorant, "VAL"),
            new(SupportedGames.Overwatch2.Name, Page(OptimizationArea.Overwatch2, "Overwatch 2 (Steam)",
                "Cache de shaders do Overwatch na Steam e verificação do jogo. As configurações pessoais do jogo não são editadas."),
                [LibraryPlatform.Windows, LibraryPlatform.Steam], SupportedGames.Overwatch2, "OW"),
            new(SupportedGames.Deadlock.Name, Page(OptimizationArea.Deadlock, "Deadlock — ajustes compartilhados",
                "Cache de shaders do Deadlock na Steam e verificação do jogo. Os perfis do gameinfo.gi ficam na outra aba."),
                [LibraryPlatform.Windows, LibraryPlatform.Steam], SupportedGames.Deadlock, "DL"),
            new("Hardware e diagnóstico", Page(OptimizationArea.Hardware, "Hardware — somente diagnóstico",
                "Orientações sobre memória RAM, XMP/EXPO, BIOS, chipset e Resizable BAR. Só diagnóstico: o app nunca altera BIOS, XMP, overclock ou undervolt."),
                [], glyph: "\uE950")
        ];
        SelectedSection = Sections[0];
    }

    public ObservableCollection<LibrarySection> Sections { get; }

    /// <summary>Falha de leitura em linguagem simples, mostrada no Banner da página (nulo quando está tudo certo).</summary>
    [ObservableProperty]
    private ProblemInfo? _problem;

    [ObservableProperty]
    private LibrarySection _selectedSection;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Detectando jogos e lendo o estado do sistema…";

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;
        _initialized = true;
        await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        Problem = null;
        IsBusy = true;
        try
        {
            var games = await Task.Run(() => SupportedGames.All.Select(_locator.Locate).ToArray());
            var steamRoot = _locator.Steam.SteamRoot;
            LoadArtwork(steamRoot);
            foreach (var section in Sections)
            {
                var detected = section.Game is null ? null : games.First(game => game.Game.Id == section.Game.Id);
                section.Subtitle = detected is null
                    ? string.Empty
                    : detected.IsInstalled ? "Instalado" : "Não encontrado";
                section.Page.DetectionText = detected switch
                {
                    null => string.Empty,
                    { IsInstalled: false } => detected.Game.Platform == GamePlatform.Riot
                        ? "Não encontrado: o Riot Client não registra uma instalação do VALORANT neste PC. As orientações abaixo continuam válidas."
                        : "Não encontrado nas bibliotecas da Steam deste PC.",
                    _ => $"Instalado em {detected.InstallPath}" + (detected.ExecutablePath is null ? " (executável não encontrado)" : string.Empty)
                };
                await section.Page.LoadActionsAsync(detected, steamRoot);
            }

            int? enabledStartup = null;
            try { enabledStartup = (await _startup.GetStartupItemsAsync()).Count(item => item.IsEnabled); }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { }

            StatusText = "Lendo o estado do Windows, dos discos e do hardware (somente leitura)…";
            var items = await _diagnostics.RunAsync(games, enabledStartup);
            foreach (var section in Sections)
                section.Page.SetDiagnostics(items);
            var attention = items.Count(item => item.Status == DiagnosticStatus.Attention);
            StatusText = $"{games.Count(game => game.IsInstalled)} de {games.Length} jogos encontrados · {attention} ponto(s) de atenção. " +
                         "Nada é alterado até você marcar ações e clicar em Executar.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.IO.IOException or System.Text.Json.JsonException)
        {
            Problem = ProblemInfo.FromException(ex, "O diagnóstico não foi concluído");
            StatusText = "Diagnóstico incompleto: as ações continuam disponíveis, mas alguns pontos de atenção não foram verificados.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LoadArtwork(string? steamRoot)
    {
        var steamIcon = steamRoot is null ? null : LocalArtwork.ExecutableIcon(Path.Combine(steamRoot, "steam.exe"));
        var riotIcon = LocalArtwork.ExecutableIcon(LocalArtwork.RiotClientPath());
        foreach (var section in Sections)
        {
            section.Icon = section.Game?.SteamAppId is { } appId
                ? LocalArtwork.SteamAppIcon(steamRoot, appId)
                : section.PlatformKinds is [LibraryPlatform.Steam] ? steamIcon : null;
            section.Platforms = section.PlatformKinds.Select(platform => platform switch
            {
                LibraryPlatform.Steam => new PlatformBadge(platform, "Steam", steamIcon),
                LibraryPlatform.Riot => new PlatformBadge(platform, "Riot Client", riotIcon),
                _ => new PlatformBadge(platform, "Windows", null)
            }).ToArray();
        }
    }

    private bool CanRefresh() => !IsBusy;
}
