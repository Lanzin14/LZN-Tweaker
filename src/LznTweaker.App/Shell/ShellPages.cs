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

    /// <summary>
    /// Menu agrupado pelo que o usuário quer fazer: Windows, Jogos e Ferramentas. Seção vazia não tem título
    /// (Início no topo; Histórico fixo no rodapé da sidebar, ver <see cref="FooterKeys"/>).
    /// </summary>
    public static IReadOnlyList<NavItem> All { get; } =
    [
        new(Home, "Início", "\uE80F", "", typeof(HomeView)),
        new(General, "Ajustes", "\uE713", "Windows", typeof(GeneralPageView)),
        new(Debloat, "Remover apps", "\uE74D", "Windows", typeof(AppDebloatView)),
        new(Cleanup, "Limpeza", "\uEA99", "Windows", typeof(CleanupView)),
        new(Startup, "Inicialização", "\uE768", "Windows", typeof(StartupView)),
        new(Games, "Jogos", "\uE7FC", "Jogos", typeof(GameLibraryView)),
        new(Gpu, "Placa de Vídeo", "\uE950", "Jogos", typeof(GpuView)),
        new(Tools, "Ferramentas", "\uE90F", "Ferramentas", typeof(ToolsView)),
        new(Backups, "Histórico", "\uE81C", "", typeof(BackupsView)),
    ];

    /// <summary>Páginas que ficam no rodapé da sidebar, separadas do menu principal.</summary>
    public static IReadOnlySet<string> FooterKeys { get; } = new HashSet<string>([Backups]);
}
