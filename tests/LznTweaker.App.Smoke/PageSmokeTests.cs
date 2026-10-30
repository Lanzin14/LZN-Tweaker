using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LznTweaker.Composition;
using LznTweaker.Core.Execution;
using LznTweaker.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace LznTweaker.UiSmoke;

/// <summary>
/// Abre a janela principal fora da tela, navega por todas as páginas da sidebar e falha se houver exceção
/// (recurso ou estilo ausente, XAML inválido) ou erro de binding. Nada é aplicado ao sistema: os scripts que
/// pediriam elevação falham sem abrir o UAC. Os prints de cada página ficam em %TEMP%\lzn-smoke para conferência.
/// </summary>
public sealed class PageSmokeTests
{
    [Fact]
    public void Every_page_opens_without_xaml_resource_or_binding_errors()
    {
        Exception? failure = null;
        var errors = new List<string>();
        var thread = new Thread(() =>
        {
            try { errors.AddRange(RunSmoke()); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromMinutes(3)), "O teste de fumaça excedeu 3 minutos.");
        if (failure is not null)
            throw new InvalidOperationException("Falha ao montar a interface", failure);
        Assert.True(errors.Count == 0, "Erros na interface:\n" + string.Join("\n", errors));
    }

    private static List<string> RunSmoke()
    {
        var errors = new List<string>();
        var bindingErrors = new BindingErrorListener();
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(bindingErrors);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;

        var app = new App();
        app.InitializeComponent();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        app.DispatcherUnhandledException += (_, e) => { errors.Add("UI: " + e.Exception.Message); e.Handled = true; };

        var services = new ServiceCollection().AddLznTweaker();
        // Sem UAC: scripts que exigiriam elevação falham em vez de abrir o pedido de administrador.
        services.AddSingleton(sp => new ScriptRunnerService(sp.GetRequiredService<ElevatedWorkspace>(), allowElevationPrompt: false));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        var window = provider.GetRequiredService<MainWindow>();
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -20000;
        window.Top = 0;
        window.Width = 1400;
        window.Height = 880;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.Show();

        var output = Path.Combine(Path.GetTempPath(), "lzn-smoke");
        Directory.CreateDirectory(output);
        var shell = (ShellViewModel)window.DataContext;
        foreach (var item in shell.NavItems.ToList())
        {
            var before = bindingErrors.Messages.Count;
            shell.Navigate(item.Key);
            Pump(TimeSpan.FromSeconds(2));
            Save(window, Path.Combine(output, $"{item.Key}.png"));
            errors.AddRange(bindingErrors.Messages.Skip(before).Select(message => $"{item.Key}: {message}"));
        }

        errors.AddRange(CheckProgramsPage(provider, shell, window, bindingErrors, output));
        errors.AddRange(CheckDiscordTab(provider, shell, window, bindingErrors, output));
        errors.AddRange(CheckGpuPage(provider, shell, window, bindingErrors, output));
        errors.AddRange(CheckSidebar(shell, window, bindingErrors, output));

        window.Close();
        PresentationTraceSources.DataBindingSource.Listeners.Remove(bindingErrors);
        app.Shutdown();
        return errors;
    }

    /// <summary>Programas: filtros e busca precisam mudar a lista; o card expandido precisa renderizar sem erro de binding.</summary>
    private static IEnumerable<string> CheckProgramsPage(ServiceProvider provider, ShellViewModel shell, Window window, BindingErrorListener bindingErrors, string output)
    {
        var errors = new List<string>();
        var programs = provider.GetRequiredService<LznTweaker.Features.Programs.ProgramsViewModel>();
        shell.Navigate(ShellPages.Programs);
        Pump(TimeSpan.FromMilliseconds(500));
        var total = programs.VisibleCount;
        if (total != programs.Programs.Count)
            errors.Add($"programas: 'Todos' mostra {total} de {programs.Programs.Count}");

        foreach (var filter in programs.Filters.Skip(1))
        {
            filter.IsSelected = true;
            Pump(TimeSpan.FromMilliseconds(150));
            var expected = programs.Programs.Count(card => card.Program.Tags.Contains(filter.Tag!.Value));
            if (programs.VisibleCount != expected || expected == 0)
                errors.Add($"programas: filtro {filter.Title} mostra {programs.VisibleCount}, esperado {expected}");
        }
        programs.Filters[0].IsSelected = true;
        programs.SearchText = "lasso";
        Pump(TimeSpan.FromMilliseconds(150));
        if (programs.VisibleCount != 1)
            errors.Add($"programas: busca 'lasso' mostra {programs.VisibleCount}, esperado 1");
        programs.SearchText = "nenhum-programa";
        Pump(TimeSpan.FromMilliseconds(150));
        if (programs.VisibleCount != 0)
            errors.Add("programas: busca sem resultado deveria mostrar 0");
        programs.ClearFiltersCommand.Execute(null);

        var before = bindingErrors.Messages.Count;
        foreach (var card in programs.Programs)
            card.IsExpanded = true;
        Pump(TimeSpan.FromSeconds(1));
        Save(window, Path.Combine(output, "programs-expanded.png"));
        errors.AddRange(bindingErrors.Messages.Skip(before).Select(message => $"programas (expandido): {message}"));
        foreach (var card in programs.Programs)
            card.IsExpanded = false;
        return errors;
    }

    /// <summary>Geral › Discord: só cards de guia, aviso em destaque e o passo a passo abre sem erro de binding.</summary>
    private static IEnumerable<string> CheckDiscordTab(ServiceProvider provider, ShellViewModel shell, Window window, BindingErrorListener bindingErrors, string output)
    {
        var errors = new List<string>();
        var general = provider.GetRequiredService<LznTweaker.Features.TweakCards.GeneralPageViewModel>();
        shell.Navigate(ShellPages.General);
        Pump(TimeSpan.FromMilliseconds(500));
        var before = bindingErrors.Messages.Count;

        // Ajustes do Windows: uma aba só, com todos os cards das antigas abas agrupados por categoria.
        if (string.Join(",", general.Tabs.Select(tab => tab.Key)) != "windows,discord")
            errors.Add("geral: abas esperadas 'windows' e 'discord', encontradas " + string.Join(",", general.Tabs.Select(tab => tab.Key)));
        general.SelectTabCommand.Execute(general.Tabs[0]);
        Pump(TimeSpan.FromMilliseconds(300));
        var categorized = general.Cards.Where(card => card.Category is not null).ToList();
        if (general.VisibleCount != categorized.Count || categorized.Select(card => card.Category).Distinct().Count() != 5)
            errors.Add($"geral: mostra {general.VisibleCount} de {categorized.Count} cards em {categorized.Select(card => card.Category).Distinct().Count()} categorias, esperado 5");
        if (general.CardsView.Groups?.Count != 5)
            errors.Add($"geral: {general.CardsView.Groups?.Count ?? 0} grupos na lista, esperado 5");
        general.RiskFilter = "Alto risco";
        Pump(TimeSpan.FromMilliseconds(200));
        if (general.VisibleCount != categorized.Count(card => card.RiskText == "Alto risco"))
            errors.Add("geral: filtro de risco não corresponde aos cards");
        general.ClearFiltersCommand.Execute(null);
        Pump(TimeSpan.FromMilliseconds(300));
        Save(window, Path.Combine(output, "general-all.png"));

        general.SelectTabCommand.Execute(general.Tabs.Single(tab => tab.Key == "discord"));
        Pump(TimeSpan.FromMilliseconds(500));
        var visible = general.CardsView.Cast<LznTweaker.Features.TweakCards.CardViewModel>().ToList();
        if (visible.Count == 0 || !visible.All(card => card is LznTweaker.Features.TweakCards.GuideCardViewModel))
            errors.Add($"discord: a aba mostra {visible.Count} card(s), esperado só guias");
        if (general.SelectedTabHighlight != "IMPORTANTE SE VOCÊ USA DISCORD.")
            errors.Add("discord: aviso em destaque ausente");
        Save(window, Path.Combine(output, "general-discord.png"));
        general.OpenDetailsCommand.Execute(visible.FirstOrDefault());
        Pump(TimeSpan.FromMilliseconds(500));
        Save(window, Path.Combine(output, "general-discord-details.png"));
        general.CloseDetailsCommand.Execute(null);
        general.SelectTabCommand.Execute(general.Tabs[0]);
        errors.AddRange(bindingErrors.Messages.Skip(before).Select(message => $"discord: {message}"));
        return errors;
    }

    /// <summary>Placa de Vídeo: "Saiba mais" começa recolhido e abre; as quatro abas renderizam sem erro de binding.</summary>
    private static IEnumerable<string> CheckGpuPage(ServiceProvider provider, ShellViewModel shell, Window window, BindingErrorListener bindingErrors, string output)
    {
        var errors = new List<string>();
        var gpu = provider.GetRequiredService<LznTweaker.Features.Gpu.GpuViewModel>();
        shell.Navigate(ShellPages.Gpu);
        Pump(TimeSpan.FromSeconds(1));
        if (gpu.IsOverviewExpanded)
            errors.Add("gpu: o painel Saiba mais deveria começar recolhido");
        var before = bindingErrors.Messages.Count;
        gpu.ToggleOverviewCommand.Execute(null);
        Pump(TimeSpan.FromMilliseconds(400));
        Save(window, Path.Combine(output, "gpu-expanded.png"));
        foreach (var tab in gpu.Tabs)
        {
            tab.IsSelected = true;
            Pump(TimeSpan.FromMilliseconds(400));
            if (gpu.SelectedTabKey != tab.Key)
                errors.Add($"gpu: aba {tab.Title} não ficou ativa");
            Save(window, Path.Combine(output, $"gpu-{tab.Key}.png"));
        }
        gpu.ToggleOverviewCommand.Execute(null);
        gpu.Tabs[0].IsSelected = true;
        errors.AddRange(bindingErrors.Messages.Skip(before).Select(message => $"gpu: {message}"));
        return errors;
    }

    /// <summary>Sidebar: as seções seguem a ordem nova; recolhida, fica estreita e continua navegando sem erro de binding.</summary>
    private static IEnumerable<string> CheckSidebar(ShellViewModel shell, Window window, BindingErrorListener bindingErrors, string output)
    {
        var errors = new List<string>();
        var sections = string.Join(",", shell.NavItems.Select(item => item.Section).Distinct());
        if (sections != "Início,Otimização,Jogos e manutenção,Segurança")
            errors.Add("sidebar: seções fora da ordem esperada: " + sections);
        var before = bindingErrors.Messages.Count;
        var sidebar = (FrameworkElement)((System.Windows.Controls.Panel)window.Content).Children[0];
        var expandedWidth = sidebar.ActualWidth;
        shell.ToggleSidebarCommand.Execute(null);
        shell.Navigate(ShellPages.General);
        Pump(TimeSpan.FromMilliseconds(500));
        if (!shell.IsSidebarCollapsed || sidebar.ActualWidth >= expandedWidth / 2)
            errors.Add($"sidebar: recolhida com {sidebar.ActualWidth} px (expandida: {expandedWidth} px)");
        Save(window, Path.Combine(output, "sidebar-collapsed.png"));
        shell.ToggleSidebarCommand.Execute(null);
        Pump(TimeSpan.FromMilliseconds(300));
        if (shell.IsSidebarCollapsed || sidebar.ActualWidth != expandedWidth)
            errors.Add("sidebar: não voltou a expandir");
        errors.AddRange(bindingErrors.Messages.Skip(before).Select(message => $"sidebar: {message}"));
        return errors;
    }

    private static void Pump(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void Save(Window window, string path)
    {
        var content = (FrameworkElement)window.Content;
        var dpi = VisualTreeHelper.GetDpi(window);
        var bitmap = new RenderTargetBitmap((int)(content.ActualWidth * dpi.DpiScaleX), (int)(content.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    private sealed class BindingErrorListener : TraceListener
    {
        public List<string> Messages { get; } = [];
        public override void Write(string? message) { }
        public override void WriteLine(string? message)
        {
            if (message is not null)
                Messages.Add(message);
        }
    }
}
