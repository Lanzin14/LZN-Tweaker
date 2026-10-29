using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LznTweaker.Core.Changes;
using LznTweaker.Core.GameOptimization;
using LznTweaker.Core.Startup;
using LznTweaker.Infrastructure;

namespace LznTweaker.Features.GameProfiles;

public sealed partial class LibrarySection(string glyph, string title, GameOptimizationPageViewModel page, SupportedGame? game = null) : ObservableObject
{
    public string Glyph { get; } = glyph;
    public string Title { get; } = title;
    public GameOptimizationPageViewModel Page { get; } = page;
    public SupportedGame? Game { get; } = game;
    public bool IsDeadlock => Game?.Id == SupportedGames.DeadlockId;

    [ObservableProperty]
    private string _subtitle = string.Empty;
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
            new("⊞", "Windows (todos os jogos)", Page(OptimizationArea.Windows, "Windows — vale para todos os jogos",
                "Caches de shaders do DirectX e da GPU, energia, Game Bar, MPO, temporizador e discos.")),
            new("◎", "Steam (compartilhado)", Page(OptimizationArea.Steam, "Steam — opções do cliente",
                "Overlay, gravação, downloads, shaders e Steam Cloud. Os caches por jogo ficam na página de cada jogo.")),
            new("◆", SupportedGames.Cs2.Name, Page(OptimizationArea.Cs2, "Counter-Strike 2",
                "Cache de shaders da Steam (App ID 730), configurações locais em userdata e diagnóstico do cs2.exe."), SupportedGames.Cs2),
            new("◇", SupportedGames.Valorant.Name, Page(OptimizationArea.Valorant, "VALORANT",
                "Somente itens suportados pelo VALORANT. Nada do CS2 é reaproveitado e o Vanguard nunca é tocado."), SupportedGames.Valorant),
            new("◈", SupportedGames.Overwatch2.Name, Page(OptimizationArea.Overwatch2, "Overwatch 2 (Steam)",
                "Cache de shaders da Steam (App ID 2357570) e diagnóstico do Overwatch.exe. As configurações pessoais do jogo não são editadas."), SupportedGames.Overwatch2),
            new("◉", SupportedGames.Deadlock.Name, Page(OptimizationArea.Deadlock, "Deadlock — ajustes compartilhados",
                "Cache de shaders da Steam (App ID 1422450) e diagnóstico do deadlock.exe. O perfil do gameinfo.gi fica na outra aba."), SupportedGames.Deadlock),
            new("▣", "Hardware e diagnóstico", Page(OptimizationArea.Hardware, "Hardware — somente diagnóstico",
                "RAM, XMP/EXPO, BIOS, chipset, ReBAR e ferramentas de medição. O app nunca altera BIOS, XMP, overclock ou undervolt."))
        ];
        SelectedSection = Sections[0];
    }

    public ObservableCollection<LibrarySection> Sections { get; }

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
        IsBusy = true;
        try
        {
            var games = await Task.Run(() => SupportedGames.All.Select(_locator.Locate).ToArray());
            var steamRoot = _locator.Steam.SteamRoot;
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
            StatusText = $"Diagnóstico incompleto: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRefresh() => !IsBusy;
}
