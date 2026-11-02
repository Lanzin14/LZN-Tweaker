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
        // Preferências de interface numa pasta temporária: o teste nunca grava nas preferências reais do usuário.
        var uiPreferencesPath = Path.Combine(Path.GetTempPath(), $"lzn-smoke-ui-{Guid.NewGuid():N}.json");
        services.AddSingleton(new LznTweaker.Core.Preferences.UiPreferencesStore(uiPreferencesPath));
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
            // O Início vem primeiro: confere antes que a página Limpeza seja aberta e faça a varredura.
            if (item.Key == ShellPages.Home)
                errors.AddRange(CheckHomePage(provider, shell));
        }

        errors.AddRange(CheckProgramsPage(provider, shell, window, bindingErrors, output));
        errors.AddRange(CheckDiscordTab(provider, shell, window, bindingErrors, output));
        errors.AddRange(CheckGpuPage(provider, shell, window, bindingErrors, output));
        errors.AddRange(CheckFortnitePage(provider, shell, window, bindingErrors, output));
        errors.AddRange(CheckGameTabs(provider, shell, bindingErrors));
        errors.AddRange(CheckToolsPage(provider, shell, window, bindingErrors, output));
        errors.AddRange(CheckGeneralList(provider, shell, window, bindingErrors, output));
        errors.AddRange(CheckSidebar(shell, window, bindingErrors, output));

        window.Close();
        PresentationTraceSources.DataBindingSource.Listeners.Remove(bindingErrors);
        app.Shutdown();
        return errors;
    }

    /// <summary>
    /// Início: não varre a Limpeza sozinho (deixaria a abertura lenta) e "Revisar recomendados" só marca a seleção
    /// recomendada e abre Ajustes. O teste descarta a marcação; nada é aplicado.
    /// </summary>
    private static IEnumerable<string> CheckHomePage(ServiceProvider provider, ShellViewModel shell)
    {
        var errors = new List<string>();
        var home = provider.GetRequiredService<LznTweaker.Features.Home.HomeViewModel>();
        var general = provider.GetRequiredService<LznTweaker.Features.TweakCards.GeneralPageViewModel>();
        for (var waited = 0; home.TweaksBadge == "lendo…" && waited < 50; waited++)
            Pump(TimeSpan.FromMilliseconds(100));
        if (home.CleanupBadge != "não verificado")
            errors.Add($"início: a Limpeza não deveria ser varrida ao abrir o app (selo: {home.CleanupBadge})");
        var missing = home.RecommendedMissing;
        home.ReviewRecommendedCommand.Execute(null);
        Pump(TimeSpan.FromMilliseconds(500));
        if (shell.SelectedItem.Key != ShellPages.General)
            errors.Add($"início: Revisar recomendados abriu {shell.SelectedItem.Key}, esperado {ShellPages.General}");
        if (general.HasPending != missing > 0)
            errors.Add($"início: {missing} recomendado(s) faltando, mas pendências em Ajustes = {general.HasPending}");
        general.DiscardCommand.Execute(null);
        Pump(TimeSpan.FromMilliseconds(200));
        if (general.HasPending)
            errors.Add("início: a marcação dos recomendados não foi descartada");
        return errors;
    }

    /// <summary>Jogos: cada aba de cada jogo abre sem erro de binding; a barra "Executar marcadas" só aparece em Ações.</summary>
    private static IEnumerable<string> CheckGameTabs(ServiceProvider provider, ShellViewModel shell, BindingErrorListener bindingErrors)
    {
        var errors = new List<string>();
        var library = provider.GetRequiredService<LznTweaker.Features.GameProfiles.GameLibraryViewModel>();
        shell.Navigate(ShellPages.Games);
        Pump(TimeSpan.FromMilliseconds(500));
        var before = bindingErrors.Messages.Count;
        foreach (var section in library.Sections)
        {
            library.SelectedSection = section;
            Pump(TimeSpan.FromMilliseconds(300));
            var page = section.Page;
            if (page.Tabs.Count == 0 || page.Tabs.All(tab => tab.Key != LznTweaker.Features.GameProfiles.GameOptimizationPageViewModel.GuidanceTab))
                errors.Add($"jogos: {section.Title} sem a aba Orientações");
            foreach (var tab in page.Tabs)
            {
                page.SelectTabCommand.Execute(tab.Key);
                Pump(TimeSpan.FromMilliseconds(150));
                if (page.SelectedTab != tab.Key)
                    errors.Add($"jogos: {section.Title}, aba {tab.Title} não ficou ativa");
                if (tab.Key == LznTweaker.Features.GameProfiles.GameOptimizationPageViewModel.SettingsTab && page.RecommendationsUpdatedText.Length == 0)
                    errors.Add($"jogos: {section.Title} sem a data das recomendações");
                var expectsBar = tab.Key == LznTweaker.Features.GameProfiles.GameOptimizationPageViewModel.ActionsTab && page.HasActions;
                if (page.ShowsActionBar != expectsBar)
                    errors.Add($"jogos: {section.Title}, aba {tab.Title}: barra de ação {page.ShowsActionBar}, esperado {expectsBar}");
            }
        }
        library.SelectedSection = library.Sections[0];
        errors.AddRange(bindingErrors.Messages.Skip(before).Select(message => $"jogos (abas): {message}"));
        return errors;
    }

    /// <summary>Programas: filtros e busca precisam mudar a lista; o detalhe de cada programa precisa renderizar sem erro de binding.</summary>
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

        // Lista + detalhe: cada programa abre no detalhe sem erro de binding; a seleção acompanha o filtro.
        var before = bindingErrors.Messages.Count;
        if (programs.SelectedProgram is null)
            errors.Add("programas: com a lista cheia, um programa deveria estar selecionado");
        foreach (var card in programs.Programs)
        {
            programs.SelectedProgram = card;
            Pump(TimeSpan.FromMilliseconds(150));
        }
        Save(window, Path.Combine(output, "programs-expanded.png"));
        errors.AddRange(bindingErrors.Messages.Skip(before).Select(message => $"programas (detalhe): {message}"));
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
        if (general.VisibleCount != categorized.Count || categorized.Select(card => card.Category).Distinct().Count() != 6)
            errors.Add($"geral: mostra {general.VisibleCount} de {categorized.Count} cards em {categorized.Select(card => card.Category).Distinct().Count()} categorias, esperado 6");
        if (general.CardsView.Groups?.Count != 6)
            errors.Add($"geral: {general.CardsView.Groups?.Count ?? 0} grupos na lista, esperado 6");
        general.RiskFilter = "Alto risco";
        Pump(TimeSpan.FromMilliseconds(200));
        if (general.VisibleCount != categorized.Count(card => card.RiskText == "Alto risco"))
            errors.Add("geral: filtro de risco não corresponde aos cards");
        general.ClearFiltersCommand.Execute(null);
        Pump(TimeSpan.FromMilliseconds(300));
        // Perfil pronto só marca (fica pendente) e "Limpar" desfaz a marcação; nada é aplicado.
        if (!general.ShowsPresetBar)
            errors.Add("geral: perfis prontos ausentes na aba Windows");
        general.ApplyPresetCommand.Execute(LznTweaker.Core.Tweaks.TweakPreset.Advanced);
        Pump(TimeSpan.FromMilliseconds(300));
        Save(window, Path.Combine(output, "general-preset.png"));
        general.DiscardCommand.Execute(null);
        Pump(TimeSpan.FromMilliseconds(200));
        if (general.HasPending)
            errors.Add("geral: Limpar deveria descartar o perfil marcado");
        Save(window, Path.Combine(output, "general-all.png"));

        // Como o RadioButton faz: marca a aba nova com a antiga ainda marcada. A lista precisa trocar sem atualizar.
        general.Tabs.Single(tab => tab.Key == "discord").IsSelected = true;
        Pump(TimeSpan.FromMilliseconds(500));
        if (general.Tabs.Count(tab => tab.IsSelected) != 1)
            errors.Add("discord: mais de uma aba ficou marcada");
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

    /// <summary>
    /// Jogos › Fortnite: nenhuma fabricante vem marcada (a placa nunca é detectada), os dois perfis de vídeo e as
    /// seções NVIDIA e AMD renderizam sem erro de binding. Nada é aplicado.
    /// </summary>
    private static IEnumerable<string> CheckFortnitePage(ServiceProvider provider, ShellViewModel shell, Window window, BindingErrorListener bindingErrors, string output)
    {
        var errors = new List<string>();
        var library = provider.GetRequiredService<LznTweaker.Features.GameProfiles.GameLibraryViewModel>();
        shell.Navigate(ShellPages.Games);
        Pump(TimeSpan.FromSeconds(1));
        var section = library.Sections.SingleOrDefault(item => item.Game?.Id == LznTweaker.Core.GameOptimization.SupportedGames.FortniteId);
        if (section is null)
            return ["fortnite: seção ausente na biblioteca de jogos"];
        var before = bindingErrors.Messages.Count;
        library.SelectedSection = section;
        Pump(TimeSpan.FromMilliseconds(500));
        var page = section.Page;
        if (!page.IsFortnite || page.HasGpuChoice || page.GpuSettings.Count != 0)
            errors.Add("fortnite: a fabricante da placa deveria começar sem escolha");
        if (!page.UsesPerformanceMode || page.FortniteVideoSettings.Count == 0)
            errors.Add("fortnite: o perfil competitivo deveria vir selecionado");
        if (!section.Platforms.Any(badge => badge.Name == "Epic Games"))
            errors.Add("fortnite: selo da Epic Games ausente");
        Save(window, Path.Combine(output, "games-fortnite.png"));

        page.IsNvidiaChosen = true;
        Pump(TimeSpan.FromMilliseconds(300));
        if (!page.HasGpuChoice || page.GpuSettings.Count == 0 || page.IsAmdChosen)
            errors.Add("fortnite: a seção NVIDIA não abriu");
        Save(window, Path.Combine(output, "games-fortnite-nvidia.png"));
        page.IsAmdChosen = true;
        page.UsesDirectX12 = true;
        Pump(TimeSpan.FromMilliseconds(300));
        if (page.IsNvidiaChosen || page.UsesPerformanceMode || page.GpuSettings.Count == 0)
            errors.Add("fortnite: AMD e DirectX 12 não ficaram ativos");
        Save(window, Path.Combine(output, "games-fortnite-amd-dx12.png"));
        page.UsesPerformanceMode = true;
        errors.AddRange(bindingErrors.Messages.Skip(before).Select(message => $"fortnite: {message}"));
        return errors;
    }

    /// <summary>Ferramentas: as quatro abas abrem sem erro de binding; navegar para Reparos/Rede/Hardware/Programas cai na aba certa.</summary>
    private static IEnumerable<string> CheckToolsPage(ServiceProvider provider, ShellViewModel shell, Window window, BindingErrorListener bindingErrors, string output)
    {
        var errors = new List<string>();
        var tools = provider.GetRequiredService<LznTweaker.Features.Tools.ToolsViewModel>();
        var before = bindingErrors.Messages.Count;
        if (tools.Repairs.Sum(group => group.Rows.Count) != 6 || tools.Network.Sum(group => group.Rows.Count) != 4)
            errors.Add($"ferramentas: {tools.Repairs.Sum(group => group.Rows.Count)} reparos e {tools.Network.Sum(group => group.Rows.Count)} itens de rede, esperado 6 e 4");
        foreach (var tab in tools.Tabs)
        {
            shell.Navigate(tab.Key);
            Pump(TimeSpan.FromMilliseconds(600));
            if (shell.SelectedItem.Key != ShellPages.Tools || tools.SelectedTabKey != tab.Key)
                errors.Add($"ferramentas: navegar para {tab.Key} não abriu a aba certa");
            Save(window, Path.Combine(output, $"tools-{tab.Key}.png"));
        }
        tools.SelectTabCommand.Execute(ShellPages.Fixes);
        errors.AddRange(bindingErrors.Messages.Skip(before).Select(message => $"ferramentas: {message}"));
        return errors;
    }

    /// <summary>Geral: lista com detalhe fixo na janela larga; na estreita, o detalhe abre por cima. Nada é aplicado.</summary>
    private static IEnumerable<string> CheckGeneralList(ServiceProvider provider, ShellViewModel shell, Window window, BindingErrorListener bindingErrors, string output)
    {
        var errors = new List<string>();
        var general = provider.GetRequiredService<LznTweaker.Features.TweakCards.GeneralPageViewModel>();
        shell.Navigate(ShellPages.General);
        general.SelectTabCommand.Execute(general.Tabs[0]);
        Pump(TimeSpan.FromMilliseconds(500));
        var before = bindingErrors.Messages.Count;
        if (!general.UsesListLayout || !general.IsDetailDocked)
            errors.Add($"geral: lista {general.UsesListLayout}, detalhe fixo {general.IsDetailDocked} na janela de {window.Width} px");
        if (general.SelectedCard is null || general.IsDetailOverlayOpen)
            errors.Add("geral: com o detalhe fixo, um item deveria estar selecionado e o painel sobreposto fechado");
        var visible = general.CardsView.Cast<LznTweaker.Features.TweakCards.CardViewModel>().ToList();
        general.SelectedCard = visible.OfType<LznTweaker.Features.TweakCards.QuickActionCardViewModel>().FirstOrDefault() ?? visible.Last();
        Pump(TimeSpan.FromMilliseconds(300));
        general.SelectedCard = visible[Math.Min(2, visible.Count - 1)];
        Pump(TimeSpan.FromMilliseconds(400));
        Save(window, Path.Combine(output, "general-list.png"));

        // Botão principal do detalhe (layout Foco): marca a alteração como pendente e, de novo, cancela. Nada é aplicado.
        var toggle = general.Cards.OfType<LznTweaker.Features.TweakCards.TweakCardViewModel>().FirstOrDefault(card => card.IsToggle && card.CanPrimaryAction);
        if (toggle is null)
            errors.Add("geral: nenhum ajuste com botão principal disponível");
        else
        {
            general.SelectedCard = toggle;
            general.PrimaryActionCommand.Execute(toggle);
            Pump(TimeSpan.FromMilliseconds(300));
            if (!toggle.IsPending || !general.HasPending || toggle.PrimaryActionText != "Cancelar alteração")
                errors.Add($"geral: o botão principal deveria deixar o ajuste pendente (pendente {toggle.IsPending}, texto {toggle.PrimaryActionText})");
            Save(window, Path.Combine(output, "general-pending.png"));
            general.PrimaryActionCommand.Execute(toggle);
            Pump(TimeSpan.FromMilliseconds(300));
            if (toggle.IsPending || general.HasPending)
                errors.Add("geral: o botão principal, de novo, deveria cancelar a alteração");
        }

        general.SearchText = "nenhum-ajuste";
        Pump(TimeSpan.FromMilliseconds(200));
        if (general.SelectedCard is not null && general.VisibleCount == 0)
            errors.Add("geral: a busca sem resultado deveria limpar a seleção do detalhe fixo");
        general.ClearFiltersCommand.Execute(null);
        Pump(TimeSpan.FromMilliseconds(200));
        if (general.SelectedCard is null)
            errors.Add("geral: ao limpar a busca, o detalhe fixo deveria voltar a mostrar um item");

        window.Width = 1000;
        Pump(TimeSpan.FromMilliseconds(500));
        if (general.IsDetailDocked || !general.IsDetailOverlayOpen)
            errors.Add("geral: em janela estreita, o detalhe deveria abrir por cima da lista");
        Save(window, Path.Combine(output, "general-narrow.png"));
        general.CloseDetailsCommand.Execute(null);
        window.Width = 1400;
        Pump(TimeSpan.FromMilliseconds(500));
        errors.AddRange(bindingErrors.Messages.Skip(before).Select(message => $"geral (lista): {message}"));
        return errors;
    }

    /// <summary>Sidebar: as seções seguem a ordem nova; recolhida, fica estreita e continua navegando sem erro de binding.</summary>
    private static IEnumerable<string> CheckSidebar(ShellViewModel shell, Window window, BindingErrorListener bindingErrors, string output)
    {
        var errors = new List<string>();
        var sections = string.Join(",", shell.NavItems.Select(item => item.Section).Where(section => section.Length > 0).Distinct());
        if (sections != "Windows,Jogos,Ferramentas")
            errors.Add("sidebar: seções fora da ordem esperada: " + sections);
        if (shell.NavView.Cast<LznTweaker.Shell.NavItem>().Any(item => item.Key == ShellPages.Backups) || shell.FooterNavItems.Single().Key != ShellPages.Backups)
            errors.Add("sidebar: o Histórico deveria ficar só no rodapé");
        shell.Navigate(ShellPages.Backups);
        if (shell.SelectedMainItem is not null || shell.SelectedFooterItem?.Key != ShellPages.Backups)
            errors.Add("sidebar: com o Histórico aberto, só o rodapé deveria aparecer selecionado");
        var before = bindingErrors.Messages.Count;
        var sidebar = (FrameworkElement)((System.Windows.Controls.Panel)window.Content).Children[0];
        // Layout Foco: sem preferência salva, a sidebar abre recolhida.
        if (!shell.IsSidebarCollapsed)
            errors.Add("sidebar: deveria começar recolhida");
        shell.Navigate(ShellPages.General);
        Pump(TimeSpan.FromMilliseconds(400));
        var collapsedWidth = sidebar.ActualWidth;
        Save(window, Path.Combine(output, "sidebar-collapsed.png"));
        shell.ToggleSidebarCommand.Execute(null);
        Pump(TimeSpan.FromMilliseconds(400));
        if (shell.IsSidebarCollapsed || sidebar.ActualWidth <= collapsedWidth * 2)
            errors.Add($"sidebar: expandida com {sidebar.ActualWidth} px (recolhida: {collapsedWidth} px)");
        Save(window, Path.Combine(output, "sidebar-expanded.png"));
        shell.ToggleSidebarCommand.Execute(null);
        Pump(TimeSpan.FromMilliseconds(300));
        if (!shell.IsSidebarCollapsed || sidebar.ActualWidth != collapsedWidth)
            errors.Add("sidebar: não voltou a recolher");
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
