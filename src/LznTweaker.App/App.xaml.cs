using System.Windows;
using LznTweaker.Composition;
using Microsoft.Extensions.DependencyInjection;

namespace LznTweaker;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _services = new ServiceCollection().AddLznTweaker().BuildServiceProvider(new ServiceProviderOptions
        {
            // Detecta já na inicialização serviços registrados com dependências ausentes.
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        _services.GetRequiredService<MainWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
