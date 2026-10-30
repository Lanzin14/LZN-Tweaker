using System.Net.Http;
using LznTweaker.Core;
using LznTweaker.Core.Changes;
using LznTweaker.Core.Cleanup;
using LznTweaker.Core.Debloat;
using LznTweaker.Core.Execution;
using LznTweaker.Core.GameOptimization;
using LznTweaker.Core.GameTuning;
using LznTweaker.Core.Gpu;
using LznTweaker.Core.Maintenance;
using LznTweaker.Core.Startup;
using LznTweaker.Core.Tweaks;
using LznTweaker.Features.Backups;
using LznTweaker.Features.Debloat;
using LznTweaker.Features.GameProfiles;
using LznTweaker.Features.Gpu;
using LznTweaker.Features.Hardware;
using LznTweaker.Features.Home;
using LznTweaker.Features.Programs;
using LznTweaker.Features.TweakCards;
using LznTweaker.Infrastructure;
using LznTweaker.Shell;
using LznTweaker.ViewModels;
using LznTweaker.Views;
using Microsoft.Extensions.DependencyInjection;

namespace LznTweaker.Composition;

/// <summary>Único lugar onde serviços do Core, ViewModels e Views são ligados entre si.</summary>
public static class ServiceRegistration
{
    public static IServiceCollection AddLznTweaker(this IServiceCollection services)
    {
        // Execução e histórico
        services.AddSingleton(_ => ElevatedWorkspace.CreateDefault());
        services.AddSingleton<ScriptRunnerService>();
        services.AddSingleton(_ => new TweakOperationHistory());
        services.AddSingleton<RestorePointGuard>();

        // Serviços de domínio
        services.AddSingleton<TweakBatchExecutor>();
        services.AddSingleton<TempCleanupPreviewService>();
        services.AddSingleton<SystemInformationService>();
        services.AddSingleton<SystemCleanupService>();
        services.AddSingleton<StartupManagerService>();
        services.AddSingleton(_ => CreateHttpClient());
        services.AddSingleton<SteamLibraryLocator>();
        services.AddSingleton<GameTuningService>();
        services.AddSingleton<AppxDebloatService>();
        services.AddSingleton<GameLocator>();
        services.AddSingleton<GamingDiagnosticsService>();
        services.AddSingleton<ShaderCacheService>();
        services.AddSingleton<SteamFolderBackupService>();
        services.AddSingleton<SystemToolsService>();
        services.AddSingleton<GpuDetectionService>();
        services.AddSingleton<GpuPreferencesStore>();

        // Infraestrutura da interface
        services.AddSingleton<IUserDialogs, UserDialogs>();
        services.AddSingleton<RestorePointPrompt>();
        services.AddSingleton<ITempCleanupPreviewDialog, TempCleanupPreviewDialog>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton<INavigationService>(provider => provider.GetRequiredService<NavigationService>());
        services.AddSingleton<TweakCardsServices>();

        // Páginas: uma instância por sessão, para preservar o estado ao navegar.
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<HomeView>();
        services.AddSingleton<BackupsViewModel>();
        services.AddSingleton<BackupsView>();
        services.AddSingleton<GeneralPageViewModel>();
        services.AddSingleton<GeneralPageView>();
        services.AddSingleton<NetworkPageViewModel>();
        services.AddSingleton<NetworkPageView>();
        services.AddSingleton<FixesPageViewModel>();
        services.AddSingleton<FixesPageView>();
        services.AddSingleton<HardwareViewModel>();
        services.AddSingleton<HardwareView>();
        services.AddSingleton<GpuViewModel>();
        services.AddSingleton<GpuView>();
        services.AddSingleton<TweaksViewModel>();
        services.AddSingleton<TweaksView>();
        services.AddSingleton<CleanupViewModel>();
        services.AddSingleton<CleanupView>();
        services.AddSingleton<StartupViewModel>();
        services.AddSingleton<StartupView>();
        services.AddSingleton<DeadlockTuningViewModel>();
        services.AddSingleton<DeadlockTuningView>();
        services.AddSingleton<GameLibraryViewModel>();
        services.AddSingleton<GameLibraryView>();
        services.AddSingleton<AppDebloatViewModel>();
        services.AddSingleton<AppDebloatView>();
        services.AddSingleton<ProgramsViewModel>();
        services.AddSingleton<ProgramsView>();

        // Janela principal
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<MainWindow>();
        return services;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        // A API do GitHub exige User-Agent.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LznTweaker/1.0");
        return client;
    }
}
