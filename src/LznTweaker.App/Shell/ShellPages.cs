using LznTweaker.Features.Backups;
using LznTweaker.Features.Debloat;
using LznTweaker.Features.GameProfiles;
using LznTweaker.Features.Gpu;
using LznTweaker.Features.Hardware;
using LznTweaker.Features.Home;
using LznTweaker.Features.Programs;
using LznTweaker.Features.TweakCards;
using LznTweaker.Features.Tools;
using LznTweaker.Views;

namespace LznTweaker.Shell;

/// <summary>
/// Itens da sidebar, na ordem e nas seções exibidas (a ordem da lista define a ordem das seções). Backups e Fixes
/// ficam em Segurança, longe do item Geral. Ícones: Segoe Fluent Icons / MDL2 Assets.
/// </summary>
public static class ShellPages
{
    public const string Home = "home";
    public const string Backups = "backups";
    public const string Fixes = "fixes";
    public const string General = "general";
    public const string Hardware = "hardware";
    public const string Gpu = "gpu";
    public const string Debloat = "debloat";
    public const string Network = "network";
    public const string Games = "games";
    public const string Cleanup = "cleanup";
    public const string Startup = "startup";
    public const string Programs = "programs";
    public const string Tools = "tools";

    /// <summary>Páginas que viraram abas de Ferramentas: navegar até elas abre Ferramentas na aba certa.</summary>
    public static IReadOnlySet<string> ToolTabs { get; } = new HashSet<string>([Fixes, Network, Hardware, Programs]);

    public static IReadOnlyList<NavItem> All { get; } =
    [
        new(Home, "Início", "", "Início", typeof(HomeView)),
        new(General, "Ajustes", "", "Otimização", typeof(GeneralPageView)),
        new(Gpu, "Placa de Vídeo", "", "Otimização", typeof(GpuView)),
        new(Debloat, "Remover apps", "", "Otimização", typeof(AppDebloatView)),
        new(Games, "Jogos", "", "Jogos e manutenção", typeof(GameLibraryView)),
        new(Cleanup, "Limpeza", "", "Jogos e manutenção", typeof(CleanupView)),
        new(Startup, "Inicialização", "", "Jogos e manutenção", typeof(StartupView)),
        new(Tools, "Ferramentas", "\uE90F", "Ferramentas e segurança", typeof(ToolsView)),
        new(Backups, "Histórico", "", "Ferramentas e segurança", typeof(BackupsView)),
    ];
}
