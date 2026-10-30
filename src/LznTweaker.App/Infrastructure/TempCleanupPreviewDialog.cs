using System.Windows;
using LznTweaker.Core.Tweaks;
using LznTweaker.Views;

namespace LznTweaker.Infrastructure;

/// <summary>Abre a prévia de temporários para o usuário escolher o que apagar; mantém a janela fora do ViewModel.</summary>
public interface ITempCleanupPreviewDialog
{
    /// <returns>Os arquivos escolhidos, ou nulo se a prévia foi cancelada.</returns>
    IReadOnlyList<TempCleanupFile>? Choose(IReadOnlyList<TempCleanupFile> files);
}

public sealed class TempCleanupPreviewDialog : ITempCleanupPreviewDialog
{
    public IReadOnlyList<TempCleanupFile>? Choose(IReadOnlyList<TempCleanupFile> files)
    {
        var preview = new TempCleanupPreviewWindow(files) { Owner = Application.Current?.MainWindow };
        return preview.ShowDialog() == true ? preview.SelectedFiles : null;
    }
}
