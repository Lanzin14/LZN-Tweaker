using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LznTweaker.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LznTweaker.Shell;

public sealed record NavItem(string Key, string Title, string Glyph, string Section, Type ViewType);

public sealed class ToastViewModel(Notification notification)
{
    private static SolidColorBrush Green => ThemeBrushes.Success;
    private static SolidColorBrush Amber => ThemeBrushes.Warning;
    private static SolidColorBrush Red => ThemeBrushes.Danger;
    private static SolidColorBrush Violet => ThemeBrushes.Accent;

    public string Title { get; } = notification.Title;
    public string Message { get; } = notification.Message;
    public string Glyph { get; } = notification.Kind switch
    {
        NotificationKind.Success => "",
        NotificationKind.Warning => "",
        NotificationKind.Error => "",
        _ => ""
    };
    public Brush AccentBrush { get; } = notification.Kind switch
    {
        NotificationKind.Success => Green,
        NotificationKind.Warning => Amber,
        NotificationKind.Error => Red,
        _ => Violet
    };

}

/// <summary>
/// Janela principal: itens da sidebar, página atual (instâncias únicas por sessão, então o estado de cada página
/// é preservado ao navegar), notificações e informações do rodapé.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly IServiceProvider _services;

    public ShellViewModel(IServiceProvider services, INotificationService notifications, NavigationService navigation)
    {
        _services = services;
        NavItems = new ObservableCollection<NavItem>(ShellPages.All);
        NavView = CollectionViewSource.GetDefaultView(NavItems);
        NavView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(NavItem.Section)));
        // O ShellViewModel é criado na thread da interface; notificações de outras threads voltam para ela.
        var dispatcher = Dispatcher.CurrentDispatcher;
        notifications.Raised += notification => dispatcher.BeginInvoke(() => ShowToast(notification));
        notifications.StatusChanged += status => dispatcher.BeginInvoke(() => StatusText = status);
        navigation.Handler = Navigate;
        _selectedItem = NavItems[0];
        _currentView = Resolve(_selectedItem);
    }

    public ObservableCollection<NavItem> NavItems { get; }
    public ICollectionView NavView { get; }
    public ObservableCollection<ToastViewModel> Toasts { get; } = [];

    public string AppVersion => WindowsEnvironment.AppVersion;
    public string UserName => WindowsEnvironment.UserName;
    public string UserInitial => string.IsNullOrEmpty(WindowsEnvironment.UserName) ? "?" : WindowsEnvironment.UserName[..1].ToUpperInvariant();
    public string WindowsVersion => WindowsEnvironment.WindowsVersion;
    public bool IsElevated => WindowsEnvironment.IsElevated;
    public string PrivilegeText => IsElevated ? "Administrador" : "Sem privilégios de administrador";

    [ObservableProperty]
    private string _statusText = "Pronto";

    [ObservableProperty]
    private NavItem _selectedItem;

    [ObservableProperty]
    private object _currentView;

    partial void OnSelectedItemChanged(NavItem value)
    {
        if (value is not null)
            CurrentView = Resolve(value);
    }

    public void Navigate(string pageKey)
    {
        var item = NavItems.FirstOrDefault(candidate => candidate.Key == pageKey);
        if (item is not null)
            SelectedItem = item;
    }

    [RelayCommand]
    private void DismissToast(ToastViewModel? toast)
    {
        if (toast is not null)
            Toasts.Remove(toast);
    }

    private object Resolve(NavItem item) => _services.GetRequiredService(item.ViewType);

    private void ShowToast(Notification notification)
    {
        var toast = new ToastViewModel(notification);
        Toasts.Add(toast);
        while (Toasts.Count > 3)
            Toasts.RemoveAt(0);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(notification.Kind == NotificationKind.Error ? 10 : 6) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Toasts.Remove(toast);
        };
        timer.Start();
    }
}
