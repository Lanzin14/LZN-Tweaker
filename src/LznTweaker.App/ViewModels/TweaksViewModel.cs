using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using LznTweaker.Core.Changes;
using LznTweaker.Core.Tweaks;
using LznTweaker.Infrastructure;

namespace LznTweaker.ViewModels;

public sealed record TweakPresetOption(TweakPreset Preset, string Name, string Description);

public sealed class TweakItemViewModel(TweakDefinition definition) : INotifyPropertyChanged
{
    private static SolidColorBrush GreenBrush => ThemeBrushes.Success;
    private static SolidColorBrush GreenBorder => ThemeBrushes.SuccessStroke;
    private static SolidColorBrush GreenBg => ThemeBrushes.SuccessSoft;

    private static SolidColorBrush AmberBrush => ThemeBrushes.Warning;
    private static SolidColorBrush AmberBorder => ThemeBrushes.WarningStroke;
    private static SolidColorBrush AmberBg => ThemeBrushes.WarningSoft;

    private static SolidColorBrush RedBrush => ThemeBrushes.Danger;
    private static SolidColorBrush RedBorder => ThemeBrushes.DangerStroke;
    private static SolidColorBrush RedBg => ThemeBrushes.DangerSoft;

    private static SolidColorBrush PurpleBrush => ThemeBrushes.AccentText;
    private static SolidColorBrush PurpleBorder => ThemeBrushes.AccentStroke;
    private static SolidColorBrush PurpleBg => ThemeBrushes.AccentSoft;

    private bool _isSelected;
    private string _stateText = "Estado não consultado";

    public TweakDefinition Definition { get; } = definition;
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public string StateText
    {
        get => _stateText;
        private set
        {
            if (_stateText == value)
                return;
            _stateText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StateText)));
        }
    }

    public void SetStateText(string text) => StateText = text;

    public void SetState(TweakSystemState state, string detail) =>
        StateText = $"Estado atual: {state switch
        {
            TweakSystemState.Enabled => "Aplicado",
            TweakSystemState.Disabled => "Não aplicado",
            TweakSystemState.Mixed => "Parcial",
            TweakSystemState.Unsupported => "Não suportado",
            TweakSystemState.BlockedByPolicy => "Bloqueado por política",
            _ => "Desconhecido"
        }} · {detail}";

    public string Name => Definition.Name;
    public string Description => Definition.Description;
    public string TechnicalReason => Definition.TechnicalReason;
    public string RiskLevel => Definition.RiskLevel;
    public string Compatibility => Definition.Compatibility;
    public string Category => Definition.Category switch
    {
        TweakCategory.Essential => "Essenciais",
        TweakCategory.Advanced => "Avançados",
        TweakCategory.CustomizePreferences => "Preferências",
        TweakCategory.PerformancePlans => "Planos de energia",
        _ => "Categoria desconhecida"
    };
    public string OperationKind => Definition.IsOneShotOperation ? "Ação pontual" : "Configuração";
    public string AdministratorText => Definition.RequiresAdministrator ? "Exige administrador (UAC)" : "Sem administrador";
    public string RestartText => Definition.RestartNote ?? (Definition.RequiresRestart ? "Exige reinicialização" : "Sem reinicialização");
    public string RollbackText => Definition.HasReliableRollback ? "Possui reversão confiável" : "Sem reversão automática";
    public string? SourceUrl => Definition.SourceUrl;

    public Brush RiskBrush => Definition.RiskLevel switch
    {
        "Baixo" => GreenBrush,
        "Médio" => AmberBrush,
        "Alto" => RedBrush,
        _ => PurpleBrush
    };

    public Brush RiskBorderBrush => Definition.RiskLevel switch
    {
        "Baixo" => GreenBorder,
        "Médio" => AmberBorder,
        "Alto" => RedBorder,
        _ => PurpleBorder
    };

    public Brush RiskBackgroundBrush => Definition.RiskLevel switch
    {
        "Baixo" => GreenBg,
        "Médio" => AmberBg,
        "Alto" => RedBg,
        _ => PurpleBg
    };
}

public sealed class TweaksViewModel : INotifyPropertyChanged
{
    private readonly RestorePointGuard _restorePointGuard;
    private readonly TweakBatchExecutor _batchExecutor;
    private readonly TweakOperationHistory _history;
    private readonly TempCleanupPreviewService _tempCleanupPreviewService;
    private readonly RestorePointPrompt _restorePointPrompt;
    private readonly IUserDialogs _dialogs;
    private readonly ITempCleanupPreviewDialog _tempCleanupPreview;
    private IReadOnlyDictionary<string, TweakOperationResult> _undoCandidates = new Dictionary<string, TweakOperationResult>();
    private TweakPresetOption _selectedPresetOption;
    private string _availabilityMessage = "Consultando disponibilidade…";
    private string _operationMessage = "Nenhuma operação executada nesta sessão.";
    private string _historyMessage = "Histórico local ainda não carregado.";
    private bool _isBusy;
    private bool _isChangingPreset;

    public TweaksViewModel(
        RestorePointGuard restorePointGuard,
        TweakBatchExecutor batchExecutor,
        TweakOperationHistory history,
        TempCleanupPreviewService tempCleanupPreviewService,
        RestorePointPrompt restorePointPrompt,
        IUserDialogs dialogs,
        ITempCleanupPreviewDialog tempCleanupPreview)
    {
        _restorePointGuard = restorePointGuard;
        _batchExecutor = batchExecutor;
        _history = history;
        _tempCleanupPreviewService = tempCleanupPreviewService;
        _restorePointPrompt = restorePointPrompt;
        _dialogs = dialogs;
        _tempCleanupPreview = tempCleanupPreview;

        // Mesmo fluxo de segurança das outras páginas: revisão, ponto de restauração obrigatório e só então o lote.
        ApplyPresetCommand = new RelayCommand<TweakPreset>(ApplyPreset);
        ClearSelectionCommand = new RelayCommand(ClearSelection);
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync());
        RunCommand = new AsyncRelayCommand(RunWithReviewAsync);
        UndoCommand = new AsyncRelayCommand(UndoWithReviewAsync);
        OpenProtectionCommand = new RelayCommand(OpenSystemProtection);
        ShowDetailsCommand = new RelayCommand(ShowSelectedDetails);

        Presets =
        [
            new(TweakPreset.Standard, "Padrão (recomendado)", "Seleção padrão de ajustes que este app implementa; apenas marca as opções, nada é executado."),
            new(TweakPreset.Minimal, "Mínimo", "Só Consumer Features e Telemetria, mais o ponto de restauração; apenas marca as opções."),
            new(TweakPreset.Advanced, "Avançado", "Standard mais a desativação de Recall e Copilot; apenas marca as opções."),
            new(TweakPreset.Custom, "Personalizado", "Seleção manual livre de itens.")
        ];

        Items = new ObservableCollection<TweakItemViewModel>(TweakCatalog.All.Select(definition => new TweakItemViewModel(definition)));
        EssentialItems = Items.Where(item => item.Definition.Category == TweakCategory.Essential).ToArray();
        AdvancedItems = Items.Where(item => item.Definition.Category == TweakCategory.Advanced).ToArray();
        PreferenceItems = Items.Where(item => item.Definition.Category == TweakCategory.CustomizePreferences).ToArray();
        PerformancePlanItems = Items.Where(item => item.Definition.Category == TweakCategory.PerformancePlans).ToArray();

        foreach (var item in Items)
            item.PropertyChanged += Item_PropertyChanged;

        _selectedPresetOption = Presets.First(option => option.Preset == TweakPreset.Standard);
        ApplyPreset(TweakPreset.Standard);
        UpdateCounts();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public IRelayCommand<TweakPreset> ApplyPresetCommand { get; }
    public IRelayCommand ClearSelectionCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand RunCommand { get; }
    public IAsyncRelayCommand UndoCommand { get; }
    public IRelayCommand OpenProtectionCommand { get; }
    public IRelayCommand ShowDetailsCommand { get; }
    public ObservableCollection<TweakPresetOption> Presets { get; }
    public ObservableCollection<TweakItemViewModel> Items { get; }
    public IReadOnlyList<TweakItemViewModel> EssentialItems { get; }
    public IReadOnlyList<TweakItemViewModel> AdvancedItems { get; }
    public IReadOnlyList<TweakItemViewModel> PreferenceItems { get; }
    public IReadOnlyList<TweakItemViewModel> PerformancePlanItems { get; }

    public TweakPresetOption SelectedPresetOption
    {
        get => _selectedPresetOption;
        set
        {
            if (value is null || ReferenceEquals(_selectedPresetOption, value))
                return;
            _selectedPresetOption = value;
            OnPropertyChanged();
            if (!_isChangingPreset && value.Preset != TweakPreset.Custom)
                ApplyPreset(value.Preset);
        }
    }

    public string AvailabilityMessage { get => _availabilityMessage; private set => Set(ref _availabilityMessage, value); }
    public string OperationMessage { get => _operationMessage; private set => Set(ref _operationMessage, value); }
    public string HistoryMessage { get => _historyMessage; private set => Set(ref _historyMessage, value); }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
                return;
            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanApply));
            OnPropertyChanged(nameof(CanUndoSelected));
            OnPropertyChanged(nameof(UndoSummary));
        }
    }
    public bool CanApply => !IsBusy && Items.Any(item => item.IsSelected);
    public bool CanUndoSelected => !IsBusy && Items.Any(item => item.IsSelected && _undoCandidates.ContainsKey(item.Definition.Id));
    public int SelectedCount => Items.Count(item => item.IsSelected);
    public int SelectedAdminCount => Items.Count(item => item.IsSelected && item.Definition.RequiresAdministrator);
    public int SelectedRestartCount => Items.Count(item => item.IsSelected && item.Definition.RequiresRestart);
    public int SelectedWithoutRollbackCount => Items.Count(item => item.IsSelected && !item.Definition.HasReliableRollback);
    public string SelectionSummary => $"{SelectedCount} selecionado(s) · {SelectedAdminCount} exige(m) UAC · {SelectedRestartCount} pode(m) exigir reinício · {SelectedWithoutRollbackCount} sem reversão automática";
    public string UndoSummary => CanUndoSelected
        ? $"{Items.Count(item => item.IsSelected && _undoCandidates.ContainsKey(item.Definition.Id))} item(ns) selecionado(s) têm reversão registrada."
        : "Selecione um ajuste reversível aplicado pelo LznTweaker para habilitar a reversão.";
    public IReadOnlyList<string> UndoableSelectedNames => Items
        .Where(item => item.IsSelected && _undoCandidates.ContainsKey(item.Definition.Id))
        .Select(item => item.Name)
        .ToArray();
    public string CatalogScope => $"Catálogo: {Items.Count} operações reais ({EssentialItems.Count} Essential, {AdvancedItems.Count} Advanced, {PreferenceItems.Count} Preferences, {PerformancePlanItems.Count} Planos de Energia).";

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            foreach (var item in Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UpdateItemStateAsync(item, cancellationToken);
            }
            AvailabilityMessage = "O estado real das configurações foi detectado no Windows e atualizado em cada item.";
            var entries = await _history.ReadAsync(cancellationToken);
            _undoCandidates = GetLatestUndoCandidates(entries);
            OnPropertyChanged(nameof(CanUndoSelected));
            OnPropertyChanged(nameof(UndoSummary));
            OnPropertyChanged(nameof(UndoableSelectedNames));
            var latest = entries.LastOrDefault();
            HistoryMessage = latest is null
                ? "Histórico local: nenhuma operação registrada."
                : $"Última operação: {latest.StartedAt.LocalDateTime:g} · {latest.Outcome} · {latest.Message}";
        }
        catch (Exception ex)
        {
            AvailabilityMessage = $"Não foi possível atualizar o estado: {ex.Message}";
            HistoryMessage = "O histórico local não pôde ser lido.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ClearSelection()
    {
        foreach (var item in Items)
            item.IsSelected = false;
        RefreshPresetMatch();
    }

    public async Task<IReadOnlyList<TempCleanupFile>> CreateTempCleanupPreviewAsync(CancellationToken cancellationToken = default) =>
        await _tempCleanupPreviewService.ScanAsync(cancellationToken);

    public async Task<IReadOnlyList<TweakOperationResult>> RunSelectedAsync(
        IReadOnlyList<TempCleanupFile> selectedTempFiles,
        CancellationToken cancellationToken = default)
    {
        var selected = Items.Where(item => item.IsSelected).Select(item => item.Definition).ToArray();
        if (selected.Length == 0 || IsBusy)
            return [];

        IsBusy = true;
        try
        {
            var progress = new Progress<TweakBatchProgress>(update =>
            {
                OperationMessage = update.Completed == 0
                    ? $"{update.Message} ({update.Total} operação(ões) no lote)."
                    : $"Progresso {update.Completed}/{update.Total} · {update.Name}: {update.Outcome} — {update.Message}";
            });
            var results = await _batchExecutor.RunAsync(selected, selectedTempFiles, progress, cancellationToken);
            OperationMessage = string.Join("\n", results.Select(result => $"{result.RequestedState}: {result.Outcome} — {result.Message}"));
            try
            {
                var entries = await _history.ReadAsync(cancellationToken);
                _undoCandidates = GetLatestUndoCandidates(entries);
                var latest = entries.LastOrDefault();
                HistoryMessage = latest is null
                    ? "Histórico local: nenhuma operação registrada."
                    : $"Última operação: {latest.StartedAt.LocalDateTime:g} · {latest.Outcome} · {latest.Message}";
            }
            catch (Exception ex)
            {
                HistoryMessage = $"O lote terminou; não foi possível reler o histórico local: {ex.Message}";
            }
            ApplyRunResultsToUndoCandidates(results);
            OnPropertyChanged(nameof(CanUndoSelected));
            OnPropertyChanged(nameof(UndoSummary));
            OnPropertyChanged(nameof(UndoableSelectedNames));
            try
            {
                await RefreshStatesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                AvailabilityMessage = $"O resultado do lote foi recebido; não foi possível atualizar os estados: {ex.Message}";
            }
            return results;
        }
        catch (Exception ex)
        {
            OperationMessage = $"Não foi possível confirmar o resultado da execução: {ex.Message}";
            return [];
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<IReadOnlyList<TweakOperationResult>> UndoSelectedAsync(CancellationToken cancellationToken = default)
    {
        var candidates = Items.Where(item => item.IsSelected)
            .Select(item => _undoCandidates.GetValueOrDefault(item.Definition.Id))
            .Where(entry => entry is not null)
            .Cast<TweakOperationResult>()
            .ToArray();
        if (candidates.Length == 0 || IsBusy)
            return [];

        IsBusy = true;
        try
        {
            var results = await _batchExecutor.UndoAsync(candidates, cancellationToken);
            OperationMessage = string.Join("\n", results.Select(result => $"{result.RequestedState}: {result.Outcome} — {result.Message}"));
            await RefreshAsync(cancellationToken);
            return results;
        }
        catch (Exception ex)
        {
            OperationMessage = $"Falha ao desfazer: {ex.Message}";
            return [];
        }
        finally { IsBusy = false; }
    }

    private async Task RefreshStatesAsync(CancellationToken cancellationToken)
    {
        foreach (var item in Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UpdateItemStateAsync(item, cancellationToken);
        }
    }

    /// <summary>Revisão do lote, ponto de restauração obrigatório (RestorePointPrompt) e execução.</summary>
    private async Task RunWithReviewAsync()
    {
        if (SelectedCount == 0 || IsBusy)
            return;
        if (!await _restorePointPrompt.EnsureAvailableAsync())
            return;
        // A Proteção do Sistema pode ter sido ativada agora; o item do ponto de restauração mostra o novo estado.
        await UpdateItemStateAsync(Items.First(item => item.Definition.Id == TweakCatalog.CreateRestorePointId), CancellationToken.None);

        IReadOnlyList<TempCleanupFile> selectedTempFiles = [];
        if (Items.Any(item => item.IsSelected && item.Definition.Id == TweakCatalog.DeleteTemporaryFilesId))
        {
            try
            {
                var chosen = _tempCleanupPreview.Choose(await CreateTempCleanupPreviewAsync());
                if (chosen is null)
                    return;
                selectedTempFiles = chosen;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                _dialogs.ShowError($"Não foi possível gerar a prévia: {ex.Message}", "Prévia de limpeza");
                return;
            }
        }

        if (!_dialogs.Confirm(BuildReview(selectedTempFiles), "Confirmar execução dos Tweaks"))
            return;
        await RunSelectedAsync(selectedTempFiles);
    }

    private string BuildReview(IReadOnlyList<TempCleanupFile> selectedTempFiles)
    {
        var selected = Items.Where(item => item.IsSelected).ToArray();
        var lines = string.Join(Environment.NewLine, selected.Select(item => $"• {item.Name} — risco {item.RiskLevel}"));
        var cleanupLine = selected.Any(item => item.Definition.Id == TweakCatalog.DeleteTemporaryFilesId)
            ? $"\n• Temporários escolhidos: {selectedTempFiles.Count:N0} arquivo(s), {FormatBytes(selectedTempFiles.Sum(file => file.Length))}."
            : string.Empty;
        var requiresUac = selected.Any(item => item.Definition.RequiresAdministrator);
        var withoutRollback = selected.Count(item => !item.Definition.HasReliableRollback && item.Definition.Id != TweakCatalog.CreateRestorePointId);
        return "O lote será executado em um único processo coordenado:\n\n" + lines + cleanupLine + "\n\n" +
            (requiresUac ? "• Operações administrativas usam a elevação com que o LznTweaker foi iniciado.\n" : "• Este lote não requer administrador.\n") +
            "• Antes de tudo, um ponto de restauração é criado (obrigatório). Se o Windows não criar nem houver um ponto das últimas 24 horas, nenhuma outra alteração é feita.\n" +
            "• Cada operação é registrada no histórico; falhas não interrompem operações independentes, e uma falha no meio de um ajuste desfaz as partes já aplicadas dele.\n" +
            (withoutRollback > 0
                ? $"• {withoutRollback} item(ns) não têm reversão automática pelo app (ações pontuais ou scripts de sistema).\n"
                : "• O estado anterior de todos os itens será registrado para o Undo.\n") +
            "• Nenhum reinício será feito automaticamente.\n\nDeseja executar?";
    }

    private async Task UndoWithReviewAsync()
    {
        if (!CanUndoSelected)
            return;
        var names = string.Join(Environment.NewLine, UndoableSelectedNames.Select(name => $"• {name}"));
        if (_dialogs.Confirm(
                "O app verificará se os valores ainda correspondem aos que ele aplicou. Se outro programa ou você os alterou desde então, aquela reversão será ignorada para preservar o estado atual.\n\n" +
                names + "\n\nContinuar?", "Confirmar reversão"))
            await UndoSelectedAsync();
    }

    private void OpenSystemProtection()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "SystemPropertiesProtection.exe")) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _dialogs.ShowError($"Não foi possível abrir Proteção do Sistema: {ex.Message}", "LznTweaker");
        }
    }

    private void ShowSelectedDetails()
    {
        var definition = Items.FirstOrDefault(item => item.IsSelected)?.Definition ?? TweakCatalog.GetById(TweakCatalog.CreateRestorePointId);
        _dialogs.ShowInfo(
            $"{definition.Name}\n\n" +
            $"Motivo técnico: {definition.TechnicalReason}\n\n" +
            $"Compatibilidade: {definition.Compatibility}\n" +
            $"Risco: {definition.RiskLevel}\n" +
            $"Requer administrador: {definition.RequiresAdministrator}\n" +
            $"Requer reinicialização: {definition.RequiresRestart}\n" +
            $"Reversão confiável: {definition.HasReliableRollback}" +
            (definition.SourceUrl is null ? "" : $"\n\nReferência: {definition.SourceUrl}"),
            "Detalhes do tweak");
    }

    private static string FormatBytes(long bytes) => bytes >= 1_073_741_824
        ? $"{bytes / 1_073_741_824d:0.00} GB"
        : bytes >= 1_048_576
            ? $"{bytes / 1_048_576d:0.0} MB"
            : $"{bytes / 1_024d:0} KB";

    private async Task UpdateItemStateAsync(TweakItemViewModel item, CancellationToken cancellationToken)
    {
        if (item.Definition.Id == TweakCatalog.CreateRestorePointId)
        {
            var readiness = await _restorePointGuard.CheckAsync(cancellationToken);
            item.SetStateText($"Obrigatório em todo lote · {readiness.Message}");
            return;
        }
        var (state, detail) = await Task.Run(() => TweakStateDetector.Detect(item.Definition), cancellationToken);
        item.SetState(state, detail);
    }

    private static IReadOnlyDictionary<string, TweakOperationResult> GetLatestUndoCandidates(IReadOnlyList<TweakOperationResult> entries) =>
        TweakStateDetector.GetUndoCandidates(entries);

    private void ApplyRunResultsToUndoCandidates(IReadOnlyList<TweakOperationResult> results)
    {
        var candidates = new Dictionary<string, TweakOperationResult>(_undoCandidates, StringComparer.Ordinal);
        foreach (var result in results)
        {
            var definition = TweakCatalog.All.FirstOrDefault(item => item.Id == result.TweakId);
            if (definition?.HasReliableRollback != true || !result.Outcome.StartsWith("Sucesso verificado", StringComparison.Ordinal))
                continue;

            if (string.IsNullOrWhiteSpace(result.RollbackData))
                candidates.Remove(result.TweakId);
            else
                candidates[result.TweakId] = result;
        }
        _undoCandidates = candidates;
    }

    public void ApplyPreset(TweakPreset preset)
    {
        if (preset == TweakPreset.Custom)
            return;

        _isChangingPreset = true;
        try
        {
            var selectedIds = TweakPresetCatalog.GetSelection(preset);
            foreach (var item in Items)
                item.IsSelected = selectedIds.Contains(item.Definition.Id);
            _selectedPresetOption = Presets.First(option => option.Preset == preset);
            OnPropertyChanged(nameof(SelectedPresetOption));
            UpdateCounts();
        }
        finally
        {
            _isChangingPreset = false;
        }
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TweakItemViewModel.IsSelected))
        {
            UpdateCounts();
            RefreshPresetMatch();
        }
    }

    private void RefreshPresetMatch()
    {
        var matched = TweakPresetCatalog.MatchImplementedSelection(Items.Where(item => item.IsSelected).Select(item => item.Definition.Id));
        var option = Presets.First(item => item.Preset == matched);
        if (!ReferenceEquals(_selectedPresetOption, option))
        {
            _selectedPresetOption = option;
            OnPropertyChanged(nameof(SelectedPresetOption));
        }
        UpdateCounts();
    }

    private void UpdateCounts()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedAdminCount));
        OnPropertyChanged(nameof(SelectedRestartCount));
        OnPropertyChanged(nameof(SelectedWithoutRollbackCount));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(CanUndoSelected));
        OnPropertyChanged(nameof(UndoSummary));
        OnPropertyChanged(nameof(UndoableSelectedNames));
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
