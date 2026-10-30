using LznTweaker.Features.Backups;
using LznTweaker.Features.Debloat;
using LznTweaker.Features.GameProfiles;
using LznTweaker.Features.Gpu;
using LznTweaker.Features.Hardware;
using LznTweaker.Features.Home;
using LznTweaker.Features.Programs;
using LznTweaker.Features.TweakCards;
using LznTweaker.Views;

namespace LznTweaker.Shell;

/// <summary>Itens da sidebar, na ordem e nas seções exibidas. Ícones: Segoe Fluent Icons / MDL2 Assets.</summary>
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
    public const string Advanced = "advanced";
    public const string Games = "games";
    public const string Cleanup = "cleanup";
    public const string Startup = "startup";
    public const string Programs = "programs";

    public static IReadOnlyList<NavItem> All { get; } =
    [
        new(Home, "Home", "", "Geral", typeof(HomeView)),
        new(Backups, "Backups", "", "Geral", typeof(BackupsView)),
        new(Fixes, "Fixes", "", "Geral", typeof(FixesPageView)),
        new(General, "Geral", "", "Principal", typeof(GeneralPageView)),
        new(Hardware, "Hardware", "", "Principal", typeof(HardwareView)),
        new(Gpu, "Placa de Vídeo", "", "Principal", typeof(GpuView)),
        new(Debloat, "Debloat", "", "Principal", typeof(AppDebloatView)),
        new(Network, "Network", "", "Principal", typeof(NetworkPageView)),
        new(Advanced, "Advanced", "", "Principal", typeof(TweaksView)),
        new(Games, "Jogos", "", "Jogos e manutenção", typeof(GameLibraryView)),
        new(Cleanup, "Limpeza", "", "Jogos e manutenção", typeof(CleanupView)),
        new(Startup, "Inicialização", "", "Jogos e manutenção", typeof(StartupView)),
        new(Programs, "Programas", "", "Jogos e manutenção", typeof(ProgramsView))
    ];
}
