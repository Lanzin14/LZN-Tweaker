using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using LznTweaker.Core.Tweaks;
using LznTweaker.ViewModels;

namespace LznTweaker.Views;

public partial class TweaksView : UserControl
{
    private readonly TweaksViewModel _viewModel;

    public TweaksView()
    {
        InitializeComponent();
        var history = new TweakOperationHistory();
        _viewModel = new TweaksViewModel(
            new SystemRestorePointHandler(),
            new TweakBatchExecutor(history),
            history,
            new TempCleanupPreviewService());
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.RefreshAsync();
    }

    private void PresetStandard_Click(object sender, RoutedEventArgs e) => _viewModel.ApplyPreset(TweakPreset.Standard);

    private void PresetMinimal_Click(object sender, RoutedEventArgs e) => _viewModel.ApplyPreset(TweakPreset.Minimal);

    private void PresetAdvanced_Click(object sender, RoutedEventArgs e) => _viewModel.ApplyPreset(TweakPreset.Advanced);

    private void ClearSelection_Click(object sender, RoutedEventArgs e) => _viewModel.ClearSelection();

    private async void Refresh_Click(object sender, RoutedEventArgs e) =>
        await _viewModel.RefreshAsync();

    private async void RunTweaks_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedCount == 0)
            return;

        IReadOnlyList<TempCleanupFile> selectedTempFiles = [];
        if (_viewModel.Items.Any(item => item.IsSelected && item.Definition.Id == TweakCatalog.DeleteTemporaryFilesId))
        {
            try
            {
                var files = await _viewModel.CreateTempCleanupPreviewAsync();
                var preview = new TempCleanupPreviewWindow(files) { Owner = Window.GetWindow(this) };
                if (preview.ShowDialog() != true)
                    return;
                selectedTempFiles = preview.SelectedFiles;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this), $"Não foi possível gerar a prévia: {ex.Message}", "Prévia de limpeza",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        var selected = _viewModel.Items.Where(item => item.IsSelected).ToArray();
        var lines = string.Join(Environment.NewLine, selected.Select(item => $"• {item.Name} — risco {item.RiskLevel}"));
        var cleanupLine = selected.Any(item => item.Definition.Id == TweakCatalog.DeleteTemporaryFilesId)
            ? $"\n• Temporários escolhidos: {selectedTempFiles.Count:N0} arquivo(s), {FormatBytes(selectedTempFiles.Sum(file => file.Length))}."
            : string.Empty;
        var requiresUac = selected.Any(item => item.Definition.RequiresAdministrator);
        var hasOneShot = selected.Any(item => item.Definition.IsOneShotOperation);
        var includesRestorePoint = selected.Any(item => item.Definition.Id == TweakCatalog.CreateRestorePointId);
        var restorePointNote = includesRestorePoint
            ? "• O ponto de restauração depende da Proteção do Sistema; ela não será ativada automaticamente e o Windows pode limitar a criação a uma vez por 24 horas.\n"
            : string.Empty;
        var withoutRollback = selected.Count(item => !item.Definition.HasReliableRollback);
        var choice = MessageBox.Show(
            Window.GetWindow(this),
            "O lote será executado em um único processo coordenado:\n\n" + lines + cleanupLine + "\n\n" +
            (requiresUac ? "• Operações administrativas usam a elevação com que o LznTweaker foi iniciado.\n" : "• Este lote não requer administrador.\n") +
            restorePointNote +
            "• Cada operação é registrada no histórico; falhas não interrompem operações independentes, e uma falha no meio de um ajuste desfaz as partes já aplicadas dele.\n" +
            (withoutRollback > 0
                ? $"• {withoutRollback} item(ns) não têm reversão automática pelo app (ações pontuais ou scripts de sistema).\n"
                : "• O estado anterior de todos os itens será registrado para o Undo.\n") +
            "• Nenhum reinício será feito automaticamente.\n\nDeseja executar?",
            "Confirmar execução dos Tweaks",
            MessageBoxButton.YesNo,
            hasOneShot ? MessageBoxImage.Warning : MessageBoxImage.Question,
            MessageBoxResult.No);

        if (choice != MessageBoxResult.Yes)
            return;

        await _viewModel.RunSelectedAsync(selectedTempFiles);
    }

    private async void UndoSelected_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.CanUndoSelected)
            return;

        var names = string.Join(Environment.NewLine, _viewModel.UndoableSelectedNames.Select(name => $"• {name}"));
        var choice = MessageBox.Show(
            Window.GetWindow(this),
            "O app verificará se os valores ainda correspondem aos que ele aplicou. Se outro programa ou você os alterou desde então, aquela reversão será ignorada para preservar o estado atual.\n\n" + names + "\n\nContinuar?",
            "Confirmar reversão",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (choice == MessageBoxResult.Yes)
            await _viewModel.UndoSelectedAsync();
    }

    private void OpenProtection_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var protectionPanel = System.IO.Path.Combine(Environment.SystemDirectory, "SystemPropertiesProtection.exe");
            Process.Start(new ProcessStartInfo(protectionPanel) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível abrir Proteção do Sistema: {ex.Message}", "LznTweaker",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Details_Click(object sender, RoutedEventArgs e)
    {
        var selectedItem = _viewModel.Items.FirstOrDefault(item => item.IsSelected);
        var definition = selectedItem?.Definition ?? TweakCatalog.GetById(TweakCatalog.CreateRestorePointId);
        MessageBox.Show(
            $"{definition.Name}\n\n" +
            $"Motivo técnico: {definition.TechnicalReason}\n\n" +
            $"Compatibilidade: {definition.Compatibility}\n" +
            $"Risco: {definition.RiskLevel}\n" +
            $"Requer administrador: {definition.RequiresAdministrator}\n" +
            $"Requer reinicialização: {definition.RequiresRestart}\n" +
            $"Reversão confiável: {definition.HasReliableRollback}\n\n" +
            $"Referência WinUtil: {definition.SourceUrl}",
            "Detalhes do tweak",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static string FormatBytes(long bytes) => bytes >= 1_073_741_824
        ? $"{bytes / 1_073_741_824d:0.00} GB"
        : bytes >= 1_048_576
            ? $"{bytes / 1_048_576d:0.0} MB"
            : $"{bytes / 1_024d:0} KB";
}
