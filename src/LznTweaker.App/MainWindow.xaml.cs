using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LznTweaker.Features.Debloat;
using LznTweaker.Features.GameProfiles;
using LznTweaker.ViewModels;
using LznTweaker.Views;

namespace LznTweaker;

public partial class MainWindow : Window
{
    private readonly object _dashboardContent;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly DispatcherTimer _refreshTimer;
    private readonly TweaksView _tweaksView;
    private readonly CleanupView _cleanupView;
    private readonly StartupView _startupView;
    private readonly GameLibraryView _gameLibraryView;
    private readonly AppDebloatView _appDebloatView;

    public MainWindow(DashboardViewModel dashboardViewModel, TweaksView tweaksView, CleanupView cleanupView, StartupView startupView,
        GameLibraryView gameLibraryView, AppDebloatView appDebloatView)
    {
        _appDebloatView = appDebloatView;
        InitializeComponent();
        _dashboardContent = PageHost.Content;
        _tweaksView = tweaksView;
        _cleanupView = cleanupView;
        _startupView = startupView;
        _gameLibraryView = gameLibraryView;
        _dashboardViewModel = dashboardViewModel;
        DataContext = _dashboardViewModel;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        // Atualiza só enquanto o Painel está visível, para não consultar WMI e processos em segundo plano.
        _refreshTimer.Tick += async (_, _) =>
        {
            if (ReferenceEquals(PageHost.Content, _dashboardContent))
                await _dashboardViewModel.RefreshAsync();
        };
        Loaded += async (_, _) =>
        {
            await _dashboardViewModel.RefreshAsync();
            _refreshTimer.Start();
        };
        Closed += (_, _) => _refreshTimer.Stop();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) =>
        await _dashboardViewModel.RefreshAsync();

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string page } selectedButton)
            return;

        foreach (var button in FindVisualChildren<Button>(this))
        {
            if (button.Tag is null)
                continue;

            var isSelected = ReferenceEquals(button, selectedButton);
            button.Background = isSelected ? Brush("#2B2140") : Brushes.Transparent;
            button.Foreground = isSelected ? Brush("#D8CCFF") : Brush("#AAA1B7");
        }

        BreadcrumbText.Text = page == "Painel" ? "Painel  /  Visão geral" : $"Painel  /  {page}";
        PageHost.Content = page switch
        {
            "Painel" => _dashboardContent,
            "Diagnóstico" => CreateDiagnosisPage(),
            "Tweaks" => _tweaksView,
            "Limpeza" => _cleanupView,
            "Inicialização" => _startupView,
            "Jogos" => _gameLibraryView,
            "Aplicativos" => _appDebloatView,
            _ => CreateFeaturePage(page)
        };
    }

    private void Profile_Click(object sender, RoutedEventArgs e)
    {
        var diagnosticButton = FindVisualChildren<Button>(this)
            .FirstOrDefault(button => button.Tag as string == "Diagnóstico");

        if (diagnosticButton is not null)
            Navigate_Click(diagnosticButton, e);
    }

    private UIElement CreateDiagnosisPage()
    {
        var root = new StackPanel();
        root.Children.Add(new TextBlock
        {
            Text = "Diagnóstico e recomendações",
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#F2EFF9")
        });
        root.Children.Add(new TextBlock
        {
            Text = "Sugestões geradas a partir dos dados observados neste computador.",
            FontSize = 14,
            Foreground = Brush("#AAA1B7"),
            Margin = new Thickness(0, 7, 0, 22)
        });

        root.Children.Add(new Border
        {
            Background = Brush("#1F192D"),
            BorderBrush = Brush("#362A50"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 18),
            Child = new TextBlock
            {
                Text = "Somente leitura. As recomendações explicam o contexto e não executam ajustes automaticamente.",
                Foreground = Brush("#D8CCFF"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            }
        });

        root.Children.Add(new ItemsControl
        {
            ItemsSource = _dashboardViewModel.Recommendations,
            ItemTemplate = (DataTemplate)FindResource("RecommendationTemplate")
        });
        return root;
    }

    private static UIElement CreateFeaturePage(string page)
    {
        // Diagnóstico, Tweaks, Jogos, Aplicativos, Limpeza e Inicialização têm páginas reais e nunca chegam aqui.
        var descriptions = page switch
        {
            "Privacidade" => new[]
            {
                ("Configurações disponíveis", "Consultar opções de diagnóstico, anúncios e sugestões respeitando os limites da edição do Windows."),
                ("Efeitos visíveis", "Cada opção explicará quais recursos podem ser afetados e se o Windows permite alterá-la nesta instalação.")
            },
            "Histórico" => new[]
            {
                ("Comparação antes e depois", "Guardar capturas com uso de CPU e memória, processos, espaço liberado e alterações registradas quando os dados estiverem disponíveis."),
                ("Registro verificável", "Cada operação futura terá identificador, valores anterior e novo quando possível, resultado e método de reversão."),
                ("Medições ou estimativas", "O histórico distinguirá observações reais de estimativas e não atribuirá ganhos sem condições de teste comparáveis.")
            },
            _ => new[] { ("Em desenvolvimento", "Esta área será implementada em uma etapa futura.") }
        };

        var root = new StackPanel();
        root.Children.Add(new TextBlock
        {
            Text = page,
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#F2EFF9")
        });
        root.Children.Add(new TextBlock
        {
            Text = "Área planejada para a próxima etapa do LznTweaker.",
            FontSize = 14,
            Foreground = Brush("#AAA1B7"),
            Margin = new Thickness(0, 7, 0, 22)
        });

        var notice = new Border
        {
            Background = Brush("#1F192D"),
            BorderBrush = Brush("#362A50"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 18),
            Child = new TextBlock
            {
                Text = "PROTÓTIPO · Conteúdo informativo e demonstrativo. Nenhuma alteração é aplicada ao Windows.",
                Foreground = Brush("#D8CCFF"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            }
        };
        root.Children.Add(notice);

        foreach (var (title, description) in descriptions)
        {
            var cardContent = new StackPanel();
            cardContent.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush("#F2EFF9")
            });
            cardContent.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 13,
                Foreground = Brush("#AAA1B7"),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20,
                Margin = new Thickness(0, 8, 0, 0)
            });
            root.Children.Add(new Border
            {
                Background = Brush("#15121D"),
                BorderBrush = Brush("#2B2438"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(19),
                Margin = new Thickness(0, 0, 0, 12),
                Child = cardContent
            });
        }

        return root;
    }

    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                yield return typedChild;

            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }
}
