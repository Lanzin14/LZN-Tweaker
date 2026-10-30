using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using LznTweaker.Composition;
using LznTweaker.Core.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace LznTweaker;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppLog.Initialize();
        AppLog.Info($"Iniciando LZN Tweaker {Infrastructure.WindowsEnvironment.AppVersion} · {Infrastructure.WindowsEnvironment.WindowsVersion}");
        // Qualquer exceção não tratada é registrada e explicada em vez de fechar o app sem aviso.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("Exceção fatal fora da interface", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Tarefa em segundo plano falhou sem tratamento", args.Exception);
            args.SetObserved();
        };

        base.OnStartup(e);
        try
        {
            _services = new ServiceCollection().AddLznTweaker().BuildServiceProvider(new ServiceProviderOptions
            {
                // Detecta já na inicialização serviços registrados com dependências ausentes.
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            _services.GetRequiredService<MainWindow>().Show();
        }
        catch (Exception ex)
        {
            AppLog.Error("Falha ao iniciar a janela principal", ex);
            ShowError("O LZN Tweaker não conseguiu iniciar.", ex);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLog.Info($"Encerrando (código {e.ApplicationExitCode}).");
        _services?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error("Exceção não tratada na interface", e.Exception);
        // A operação que falhou é interrompida, mas o app continua aberto; nenhuma alteração pendente é aplicada por isso.
        e.Handled = true;
        ShowError("Ocorreu um erro inesperado e a última ação foi interrompida.", e.Exception);
    }

    private static void ShowError(string summary, Exception exception)
    {
        var choice = MessageBox.Show(
            $"{summary}\n\nDetalhe: {exception.Message}\n\nO erro foi registrado em:\n{AppLog.Directory}\n\nAbrir a pasta de logs?",
            "LZN Tweaker", MessageBoxButton.YesNo, MessageBoxImage.Error, MessageBoxResult.No);
        if (choice != MessageBoxResult.Yes)
            return;
        try
        {
            System.IO.Directory.CreateDirectory(AppLog.Directory);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppLog.Directory}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Warn("Não foi possível abrir a pasta de logs", ex);
        }
    }
}
