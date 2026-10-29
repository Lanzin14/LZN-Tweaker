using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;

namespace LznTweaker.Infrastructure;

/// <summary>Diálogos usados pelas ViewModels, para que elas não dependam de MessageBox/janelas diretamente.</summary>
public interface IUserDialogs
{
    bool Confirm(string message, string title);
    void ShowInfo(string message, string title);
    void ShowError(string message, string title);
    string? PickFolder(string title, string? initialDirectory);
    void OpenUrl(string url);
}

public sealed class UserDialogs : IUserDialogs
{
    private static Window? Owner => Application.Current?.MainWindow;

    public bool Confirm(string message, string title) =>
        Owner is { } owner
            ? MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes
            : MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;

    public void ShowInfo(string message, string title) => Show(message, title, MessageBoxImage.Information);

    public void ShowError(string message, string title) => Show(message, title, MessageBoxImage.Error);

    public string? PickFolder(string title, string? initialDirectory)
    {
        var dialog = new OpenFolderDialog { Title = title, InitialDirectory = initialDirectory ?? string.Empty };
        return dialog.ShowDialog(Owner) == true ? dialog.FolderName : null;
    }

    public void OpenUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private static void Show(string message, string title, MessageBoxImage image)
    {
        if (Owner is { } owner)
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, image);
        else
            MessageBox.Show(message, title, MessageBoxButton.OK, image);
    }
}
