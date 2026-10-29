using LznTweaker.Core;
using LznTweaker.Core.Changes;
using LznTweaker.Core.Cleanup;
using System.Net.Http;
using LznTweaker.Core.Execution;
using LznTweaker.Core.GameTuning;
using LznTweaker.Features.GameProfiles;
using LznTweaker.Infrastructure;
using LznTweaker.Core.Startup;
using LznTweaker.Core.Tweaks;
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

        // Infraestrutura da interface
        services.AddSingleton<IUserDialogs, UserDialogs>();

        // Páginas: uma instância por sessão, como antes, para preservar o estado ao navegar.
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<TweaksViewModel>();
        services.AddSingleton<CleanupViewModel>();
        services.AddSingleton<StartupViewModel>();
        services.AddSingleton<TweaksView>();
        services.AddSingleton<CleanupView>();
        services.AddSingleton<StartupView>();
        services.AddSingleton<DeadlockTuningViewModel>();
        services.AddSingleton<DeadlockTuningView>();
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
