namespace LznTweaker.Infrastructure;

public enum NotificationKind { Success, Warning, Error, Info }

public sealed record Notification(string Title, string Message, NotificationKind Kind);

/// <summary>Notificações (toasts) e estado global "ocupado", exibidos pela janela principal.</summary>
public interface INotificationService
{
    event Action<Notification>? Raised;
    event Action<string>? StatusChanged;
    void Notify(string title, string message, NotificationKind kind = NotificationKind.Info);
    void SetStatus(string status);
}

public sealed class NotificationService : INotificationService
{
    public event Action<Notification>? Raised;
    public event Action<string>? StatusChanged;

    public void Notify(string title, string message, NotificationKind kind = NotificationKind.Info) =>
        Raised?.Invoke(new Notification(title, message, kind));

    public void SetStatus(string status) => StatusChanged?.Invoke(status);
}

/// <summary>Navegação entre páginas da sidebar a partir de qualquer página (ex.: atalhos da Home).</summary>
public interface INavigationService
{
    void Navigate(string pageKey);
}

/// <summary>
/// Implementação ligada pela janela principal depois de criada. Separada do ShellViewModel para evitar
/// dependência circular (Shell → páginas → navegação → Shell).
/// </summary>
public sealed class NavigationService : INavigationService
{
    public Action<string>? Handler { get; set; }
    public void Navigate(string pageKey) => Handler?.Invoke(pageKey);
}
