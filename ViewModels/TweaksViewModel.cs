using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using Microsoft.Win32;
using System.Runtime.CompilerServices;
using LznTweaker.Core.Tweaks;

namespace LznTweaker.ViewModels;

public sealed record TweakPresetOption(TweakPreset Preset, string Name, string Description);

public sealed class TweakItemViewModel(TweakDefinition definition) : INotifyPropertyChanged
{
    private static readonly SolidColorBrush GreenBrush = new((Color)ColorConverter.ConvertFromString("#7ED9B2"));
    private static readonly SolidColorBrush GreenBorder = new((Color)ColorConverter.ConvertFromString("#234837"));
    private static readonly SolidColorBrush GreenBg = new((Color)ColorConverter.ConvertFromString("#13261C"));

    private static readonly SolidColorBrush AmberBrush = new((Color)ColorConverter.ConvertFromString("#E0BF80"));
    private static readonly SolidColorBrush AmberBorder = new((Color)ColorConverter.ConvertFromString("#4F3E22"));
    private static readonly SolidColorBrush AmberBg = new((Color)ColorConverter.ConvertFromString("#292012"));

    private static readonly SolidColorBrush RedBrush = new((Color)ColorConverter.ConvertFromString("#F28B82"));
    private static readonly SolidColorBrush RedBorder = new((Color)ColorConverter.ConvertFromString("#54232A"));
    private static readonly SolidColorBrush RedBg = new((Color)ColorConverter.ConvertFromString("#2D1418"));

    private static readonly SolidColorBrush PurpleBrush = new((Color)ColorConverter.ConvertFromString("#D8CCFF"));
    private static readonly SolidColorBrush PurpleBorder = new((Color)ColorConverter.ConvertFromString("#43345E"));
    private static readonly SolidColorBrush PurpleBg = new((Color)ColorConverter.ConvertFromString("#211933"));

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
        TweakCategory.Essential => "Essential Tweaks",
        TweakCategory.Advanced => "Advanced Tweaks",
        TweakCategory.CustomizePreferences => "Customize Preferences",
        TweakCategory.PerformancePlans => "Performance Plans",
        _ => "Categoria desconhecida"
    };
    public string OperationKind => Definition.IsOneShotOperation ? "Ação pontual" : "Configuração";
    public string AdministratorText => Definition.RequiresAdministrator ? "Exige administrador (UAC)" : "Sem administrador";
    public string RestartText => Definition.RestartNote ?? (Definition.RequiresRestart ? "Exige reinicialização" : "Sem reinicialização");
    public string RollbackText => Definition.HasReliableRollback ? "Possui reversão confiável" : "Sem reversão automática";
    public string SourceUrl => Definition.SourceUrl;

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
    private readonly SystemRestorePointHandler _restorePointHandler;
    private readonly TweakBatchExecutor _batchExecutor;
    private readonly TweakOperationHistory _history;
    private readonly TempCleanupPreviewService _tempCleanupPreviewService;
    private IReadOnlyDictionary<string, TweakOperationResult> _undoCandidates = new Dictionary<string, TweakOperationResult>();
    private TweakPresetOption _selectedPresetOption;
    private string _availabilityMessage = "Consultando disponibilidade…";
    private string _operationMessage = "Nenhuma operação executada nesta sessão.";
    private string _historyMessage = "Histórico local ainda não carregado.";
    private bool _isBusy;
    private bool _isChangingPreset;

    public TweaksViewModel(
        SystemRestorePointHandler restorePointHandler,
        TweakBatchExecutor batchExecutor,
        TweakOperationHistory history,
        TempCleanupPreviewService tempCleanupPreviewService)
    {
        _restorePointHandler = restorePointHandler;
        _batchExecutor = batchExecutor;
        _history = history;
        _tempCleanupPreviewService = tempCleanupPreviewService;

        Presets =
        [
            new(TweakPreset.Standard, "Standard (Recomendado)", "Itens do preset Standard do WinUtil que este app implementa; apenas marca as opções, nada é executado."),
            new(TweakPreset.Minimal, "Mínimo", "Preset Minimal do WinUtil (Consumer Features e Telemetria) mais o ponto de restauração; apenas marca as opções."),
            new(TweakPreset.Advanced, "Avançado", "Standard mais a desativação de Recall e Copilot (itens de IA do preset Advanced do WinUtil); apenas marca as opções."),
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
                var (state, detail) = await DetectStateAsync(item.Definition, cancellationToken);
                item.SetState(state, detail);
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
            var (state, detail) = await DetectStateAsync(item.Definition, cancellationToken);
            item.SetState(state, detail);
        }
    }

    private async Task<(TweakSystemState State, string Detail)> DetectStateAsync(
        TweakDefinition definition, CancellationToken cancellationToken)
    {
        if (definition.Id == TweakCatalog.EndTaskOnTaskbarId && !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            return (TweakSystemState.Unsupported, "requer Windows 11; nada foi alterado.");
        if (definition.Id == TweakCatalog.CreateRestorePointId)
        {
            var capability = await _restorePointHandler.DetectAsync(cancellationToken);
            return (capability.State, capability.Message);
        }

        var allChanges = new List<(string SubKey, string Name, object Value, bool IsHklm)>();
        foreach (var service in definition.ServiceChanges ?? [])
        {
            // Serviços ausentes nesta versão do Windows não entram no cálculo do estado.
            using var serviceKey = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{service.Name}", writable: false);
            if (serviceKey is not null)
                allChanges.Add(($@"SYSTEM\CurrentControlSet\Services\{service.Name}", "Start", ServiceStartCode(service.TargetStartupType), true));
        }
        if (definition.RegistryChanges is { Count: > 0 })
        {
            foreach (var rc in definition.RegistryChanges)
                allChanges.Add((rc.SubKey, rc.Name, rc.Value, false));
        }
        if (definition.HklmRegistryChanges is { Count: > 0 })
        {
            foreach (var hrc in definition.HklmRegistryChanges)
                allChanges.Add((hrc.SubKey, hrc.Name, hrc.Value, true));
        }

        if (allChanges.Count == 0)
        {
            if (definition.Id == TweakCatalog.ActivateUltimatePerformanceId)
                return (TweakSystemState.Unknown, "Plano de energia de alta performance.");
            if (definition.Id == TweakCatalog.RemoveUltimatePerformanceId)
                return (TweakSystemState.Unknown, "Restaura o plano Equilibrado.");
            return (TweakSystemState.Unknown, "Nenhum método de detecção direta no Registro.");
        }

        try
        {
            var states = allChanges.Select(change =>
            {
                using var key = change.IsHklm
                    ? Registry.LocalMachine.OpenSubKey(change.SubKey, writable: false)
                    : Registry.CurrentUser.OpenSubKey(change.SubKey, writable: false);
                if (key is null || !key.GetValueNames().Contains(change.Name, StringComparer.OrdinalIgnoreCase))
                    return (Known: false, Matches: false);
                var value = key.GetValue(change.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                return (Known: true, Matches: string.Equals(Convert.ToString(value), Convert.ToString(change.Value), StringComparison.OrdinalIgnoreCase));
            }).ToArray();

            if (states.All(state => state.Known && state.Matches))
                return (TweakSystemState.Enabled, "todos os valores esperados estão aplicados.");
            if (states.All(state => state.Known && !state.Matches))
                return (TweakSystemState.Disabled, "os valores atuais diferem dos que este ajuste aplica.");
            if (states.Any(state => state.Known && state.Matches))
                return (TweakSystemState.Mixed, "valores parciais encontrados.");
            return (TweakSystemState.Unknown, "valores ausentes ou não configurados.");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            return (TweakSystemState.Unknown, $"leitura do Registro falhou: {ex.Message}");
        }
    }

    private static int ServiceStartCode(string startupType) => startupType switch
    {
        "Automatic" => 2,
        "Manual" => 3,
        _ => 4
    };

    private static IReadOnlyDictionary<string, TweakOperationResult> GetLatestUndoCandidates(IReadOnlyList<TweakOperationResult> entries)
    {
        var candidates = new Dictionary<string, TweakOperationResult>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry.RequestedState == "Desfazer")
            {
                if (entry.Outcome == "Reversão verificada")
                    candidates.Remove(entry.TweakId);
                continue;
            }

            if (entry.Outcome == "Sucesso verificado" && !string.IsNullOrWhiteSpace(entry.RollbackData))
                candidates[entry.TweakId] = entry;
        }
        return candidates;
    }

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
