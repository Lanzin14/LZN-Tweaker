using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LznTweaker.Core.Changes;
using LznTweaker.Core.GameOptimization;
using LznTweaker.Core.Tweaks;
using LznTweaker.Infrastructure;

namespace LznTweaker.Features.GameProfiles;

internal static class StatusBrushes
{
    public static SolidColorBrush Green => ThemeBrushes.Success;
    public static SolidColorBrush Amber => ThemeBrushes.Warning;
    public static SolidColorBrush Red => ThemeBrushes.Danger;
    public static SolidColorBrush Purple => ThemeBrushes.AccentText;
    public static SolidColorBrush Gray => ThemeBrushes.Muted;

}

/// <summary>Ação automatizável: limpeza de um cache de shaders ou backup e recriação do cache de shaders de um jogo na Steam.</summary>
public sealed partial class OptimizationActionViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _resultText = string.Empty;

    [ObservableProperty]
    private Brush _resultBrush = StatusBrushes.Gray;

    private OptimizationActionViewModel(string name, string description, string risk, string prerequisites, string gamesAffected, string reversibility, string state)
    {
        Name = name;
        Description = description;
        Risk = risk;
        Prerequisites = prerequisites;
        GamesAffected = gamesAffected;
        Reversibility = reversibility;
        State = state;
    }

    public string Name { get; }
    public string Description { get; }
    public string Risk { get; }
    public string Prerequisites { get; }
    public string GamesAffected { get; }
    public string Reversibility { get; }
    public string State { get; }

    /// <summary>Tamanho ocupado (o começo do estado, "1,1 MB em 26 arquivo(s) · …").</summary>
    public string SizeText => State.Split(" em ", 2)[0];

    /// <summary>Risco, alcance, pré-requisito e reversão numa dica, para a linha ficar curta.</summary>
    public string Tooltip => $"Risco {Risk} · afeta: {GamesAffected}\nAntes: {Prerequisites}\n{Reversibility}\n{State}";
    public ShaderCacheLocation? ShaderCache { get; private init; }
    public SteamFolderTarget? SteamFolder { get; private init; }

    public static OptimizationActionViewModel ForShaderCache(ShaderCacheLocation location) => new(
        $"Limpar {location.Label}",
        $"Apaga o conteúdo de {location.Path}. Os jogos vão recompilar shaders e podem travar por alguns instantes nas primeiras partidas.",
        "Baixo", "Feche os jogos (arquivos em uso são mantidos).", "Todos os jogos",
        "Sem reversão: é cache e é recriado automaticamente.",
        $"{ShaderCacheService.FormatBytes(location.Bytes)} em {Plural.Of(location.Files, "arquivo", "arquivos")} · {location.Vendor}")
    { ShaderCache = location };

    public static OptimizationActionViewModel ForSteamFolder(SteamFolderTarget target) => new($"Redefinir {target.Label}",
        $"Renomeia steamapps\\shadercache\\{target.Game.SteamAppId} para um backup; a Steam e o jogo recriam o cache.",
        "Baixo", "Steam e o jogo fechados.", target.Game.Name,
        "Reversível: \"Restaurar\" devolve a pasta original.",
        $"{ShaderCacheService.FormatBytes(target.Bytes)} em {Plural.Of(target.Files, "arquivo", "arquivos")} · {target.FolderPath}")
    { SteamFolder = target };

    public void SetResult(TweakOperationResult result)
    {
        var (label, brush) = result.Outcome switch
        {
            "Sucesso verificado" or "Reversão verificada" => ("Concluída", StatusBrushes.Green),
            "Nada a alterar" => ("Ignorada", StatusBrushes.Gray),
            "Bloqueado" => ("Pendente (bloqueada)", StatusBrushes.Amber),
            _ => ("Falha", StatusBrushes.Red)
        };
        ResultText = $"{label}: {result.Message}";
        ResultBrush = brush;
    }
}

public sealed class DiagnosticItemViewModel(DiagnosticItem item)
{
    public DiagnosticItem Item { get; } = item;
    public string Title => Item.Title;
    public string CurrentState => Item.CurrentState;
    public string Recommendation => Item.Recommendation;
    public string Details => $"Evidência: {Item.Evidence} · Risco: {Item.Risk} · Afeta: {Item.GamesAffected}" +
                             (Item.RelatedFeature is null ? string.Empty : $" · Veja: {Item.RelatedFeature}");
    public string StatusText => Item.Status switch
    {
        DiagnosticStatus.Ok => "OK",
        DiagnosticStatus.Attention => "ATENÇÃO",
        DiagnosticStatus.Info => "DEPENDE",
        _ => "NÃO LIDO"
    };
    public Brush StatusBrush => Item.Status switch
    {
        DiagnosticStatus.Ok => StatusBrushes.Green,
        DiagnosticStatus.Attention => StatusBrushes.Amber,
        DiagnosticStatus.Info => StatusBrushes.Purple,
        _ => StatusBrushes.Gray
    };
}

public sealed class GuidanceItemViewModel(GuidanceItem item)
{
    public GuidanceItem Item { get; } = item;
    public string Title => Item.Refused ? $"{Item.Title} — não aplicado" : Item.Title;
    public string Summary => Item.Summary;
    public IReadOnlyList<string> Steps => Item.Steps;
    public string Details => $"Quando: {Item.Condition} · Evidência: {Item.Evidence} · Risco: {Item.Risk} · Afeta: {Item.GamesAffected}";
    public Brush TitleBrush => Item.Refused ? StatusBrushes.Gray : StatusBrushes.Purple;
}

public sealed record NumberedStep(int Number, string Text);

/// <summary>Aba da página de um jogo, com a quantidade de itens.</summary>
public sealed record GamePageTab(string Key, string Title);

/// <summary>Procedimento manual com passos numerados para a tela.</summary>
public sealed record ProcedureView(string Title, IReadOnlyList<NumberedStep> Steps, string? Warning)
{
    public static ProcedureView From(ManualProcedure procedure) =>
        new(procedure.Title, procedure.Steps.Select((text, index) => new NumberedStep(index + 1, text)).ToArray(), procedure.Warning);
}

/// <summary>Fabricante escolhida pelo usuário na página do Fortnite (nunca detectada).</summary>
public enum FortniteGpuChoice { None, Nvidia, Amd }

/// <summary>Linha de configuração do Fortnite com a origem da recomendação.</summary>
public sealed class RecommendedSettingViewModel(RecommendedSetting setting)
{
    public string Name => setting.Name;
    public string Value => setting.Value;
    public string Purpose => setting.Purpose;
    public string? Note => setting.Note;
    public string EvidenceText => setting.Evidence switch
    {
        EvidenceKind.Official => "Oficial",
        EvidenceKind.Technical => "Técnica",
        EvidenceKind.Community => "Comunidade",
        _ => "Experimental"
    };
    public Brush EvidenceBrush => setting.Evidence switch
    {
        EvidenceKind.Official => StatusBrushes.Green,
        EvidenceKind.Technical => StatusBrushes.Purple,
        EvidenceKind.Community => StatusBrushes.Gray,
        _ => StatusBrushes.Amber
    };

    public static IReadOnlyList<RecommendedSettingViewModel> From(IEnumerable<RecommendedSetting> settings) =>
        settings.Select(setting => new RecommendedSettingViewModel(setting)).ToArray();
}

public sealed class BackupItemViewModel(SteamFolderBackup backup)
{
    public SteamFolderBackup Backup { get; } = backup;
    public string Label => Backup.Label;
    public string Details => $"Backup de {Backup.CreatedAt.LocalDateTime:g} · {Backup.BackupPath}";
}

/// <summary>Página de uma área (Windows, Steam, um jogo ou hardware). Cada instância guarda o próprio estado.</summary>
public sealed partial class GameOptimizationPageViewModel : ObservableObject
{
    private readonly OptimizationArea _area;
    private readonly ShaderCacheService _shaderCache;
    private readonly SteamFolderBackupService _steamFolders;
    private readonly RestorePointGuard _restorePointGuard;
    private readonly RestorePointPrompt _restorePointPrompt;
    private readonly IUserDialogs _dialogs;
    private string? _steamRoot;

    public GameOptimizationPageViewModel(OptimizationArea area, string title, string description,
        ShaderCacheService shaderCache, SteamFolderBackupService steamFolders, RestorePointGuard restorePointGuard,
        RestorePointPrompt restorePointPrompt, IUserDialogs dialogs)
    {
        _area = area;
        Title = title;
        Description = description;
        _shaderCache = shaderCache;
        _steamFolders = steamFolders;
        _restorePointGuard = restorePointGuard;
        _restorePointPrompt = restorePointPrompt;
        _dialogs = dialogs;
        foreach (var guidance in GuidanceCatalog.All.Where(item => item.Area == area))
            Guidance.Add(new GuidanceItemViewModel(guidance));
        Actions.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(HasActions)); OnTabsChanged(); };
        Backups.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(HasBackups)); OnTabsChanged(); };
        Diagnostics.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(HasDiagnostics)); OnTabsChanged(); };
        OnTabsChanged();
    }

    public OptimizationArea Area => _area;
    public string Title { get; }
    public string Description { get; }
    public ObservableCollection<OptimizationActionViewModel> Actions { get; } = [];
    public ObservableCollection<BackupItemViewModel> Backups { get; } = [];
    public ObservableCollection<DiagnosticItemViewModel> Diagnostics { get; } = [];
    public ObservableCollection<GuidanceItemViewModel> Guidance { get; } = [];
    public bool HasActions => Actions.Count > 0;
    public bool HasBackups => Backups.Count > 0;
    public bool HasDiagnostics => Diagnostics.Count > 0;

    // "Configurações recomendadas": instruções manuais, só na página do CS2.
    public bool HasRecommendations => _area == OptimizationArea.Cs2;
    public IReadOnlyList<ProcedureView> TaskManagerSteps => HasRecommendations
        ? Cs2Recommendations.TaskManager.Select(ProcedureView.From).ToArray()
        : [];
    public IReadOnlyList<InGameSetting> VideoSettings => HasRecommendations ? Cs2Recommendations.Video : [];
    public IReadOnlyList<string> RecommendationNotes => HasRecommendations ? Cs2Recommendations.Notes : [];
    public string VideoMenuPath => Cs2Recommendations.VideoMenuPath;

    /// <summary>Data das recomendações do jogo: menus e opções mudam com patches, então o usuário vê quando foram escritas.</summary>
    public string RecommendationsUpdatedText => (IsFortnite ? FortniteRecommendations.UpdatedOn : HasRecommendations ? Cs2Recommendations.UpdatedOn : (DateOnly?)null) is { } date
        ? $"Atualizado em {date:dd/MM/yyyy}. Patches podem mudar nomes e opções; se algo não bater, siga o que o jogo mostra."
        : string.Empty;

    // Fortnite: perfil de vídeo e fabricante escolhidos pelo usuário. A placa de vídeo nunca é detectada aqui.
    public bool IsFortnite => _area == OptimizationArea.Fortnite;
    public string FortniteVideoMenuPath => FortniteRecommendations.VideoMenuPath;
    public IReadOnlyList<string> FortniteVideoNotes => IsFortnite ? FortniteRecommendations.VideoNotes : [];
    public IReadOnlyList<string> FortniteGpuNotes => IsFortnite ? FortniteRecommendations.GpuNotes : [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsesPerformanceMode), nameof(FortniteVideoSettings), nameof(FortniteProfileSummary))]
    private bool _usesDirectX12;

    public bool UsesPerformanceMode
    {
        get => !UsesDirectX12;
        set { if (value) UsesDirectX12 = false; }
    }

    public string FortniteProfileSummary => UsesDirectX12
        ? "Alternativa para quem tem travadas, instabilidade ou imagem ruim no Modo Desempenho. Não é melhor para todos: compare os dois."
        : "Perfil competitivo recomendado pela Epic: Modo de Renderização \"Desempenho\", com menos recursos gráficos para aliviar CPU, GPU e memória.";

    public IReadOnlyList<RecommendedSettingViewModel> FortniteVideoSettings => !IsFortnite ? []
        : RecommendedSettingViewModel.From(UsesDirectX12 ? FortniteRecommendations.DirectX12 : FortniteRecommendations.PerformanceMode);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNvidiaChosen), nameof(IsAmdChosen), nameof(HasGpuChoice), nameof(GpuSettings), nameof(GpuMenuPath))]
    private FortniteGpuChoice _gpuChoice;

    public bool IsNvidiaChosen
    {
        get => GpuChoice == FortniteGpuChoice.Nvidia;
        set { if (value) GpuChoice = FortniteGpuChoice.Nvidia; }
    }

    public bool IsAmdChosen
    {
        get => GpuChoice == FortniteGpuChoice.Amd;
        set { if (value) GpuChoice = FortniteGpuChoice.Amd; }
    }

    public bool HasGpuChoice => GpuChoice != FortniteGpuChoice.None;
    public string GpuMenuPath => GpuChoice == FortniteGpuChoice.Amd ? FortniteRecommendations.AmdMenuPath : FortniteRecommendations.NvidiaMenuPath;
    public IReadOnlyList<RecommendedSettingViewModel> GpuSettings => GpuChoice switch
    {
        FortniteGpuChoice.Nvidia => RecommendedSettingViewModel.From(FortniteRecommendations.Nvidia),
        FortniteGpuChoice.Amd => RecommendedSettingViewModel.From(FortniteRecommendations.Amd),
        _ => []
    };

    /// <summary>Capa local do jogo (Steam) para o cabeçalho; nula quando o jogo não tem arte no PC.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCover))]
    private System.Windows.Media.ImageSource? _cover;

    public bool HasCover => Cover is not null;

    // ── Abas: Ações, Configurações, Orientações e Diagnóstico (só as que têm conteúdo) ──
    public const string ActionsTab = "actions";
    public const string SettingsTab = "settings";
    public const string GuidanceTab = "guidance";
    public const string DiagnosticsTab = "diagnostics";

    public IReadOnlyList<GamePageTab> Tabs { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActionsTab), nameof(IsSettingsTab), nameof(IsGuidanceTab), nameof(IsDiagnosticsTab), nameof(ShowsActionBar))]
    private string _selectedTab = GuidanceTab;

    public bool IsActionsTab => SelectedTab == ActionsTab;
    public bool IsSettingsTab => SelectedTab == SettingsTab;
    public bool IsGuidanceTab => SelectedTab == GuidanceTab;
    public bool IsDiagnosticsTab => SelectedTab == DiagnosticsTab;
    public bool ShowsActionBar => IsActionsTab && HasActions;

    public int SelectedActionsCount => Actions.Count(action => action.IsSelected);
    public string SelectedActionsText => SelectedActionsCount switch
    {
        0 => "Nenhuma ação marcada",
        1 => "1 ação marcada",
        var count => $"{count} ações marcadas"
    };

    private bool _tabChosenByUser;

    [RelayCommand]
    private void SelectTab(string? key)
    {
        if (key is null)
            return;
        _tabChosenByUser = true;
        SelectedTab = key;
    }

    private void OnTabsChanged()
    {
        var tabs = new List<GamePageTab>();
        if (HasActions || HasBackups)
            tabs.Add(new(ActionsTab, $"Ações {Actions.Count}"));
        if (HasRecommendations || IsFortnite)
            tabs.Add(new(SettingsTab, "Configurações"));
        tabs.Add(new(GuidanceTab, $"Orientações {Guidance.Count}"));
        if (HasDiagnostics)
            tabs.Add(new(DiagnosticsTab, $"Diagnóstico {Diagnostics.Count}"));
        // Até o usuário escolher, abre a primeira aba com conteúdo (as ações chegam depois da leitura do sistema).
        var keepSelection = _tabChosenByUser && tabs.Any(tab => tab.Key == SelectedTab);
        Tabs = tabs;
        OnPropertyChanged(nameof(Tabs));
        if (!keepSelection)
            SelectedTab = tabs[0].Key;
        OnPropertyChanged(nameof(ShowsActionBar));
    }

    /// <summary>Jogo desta página, quando é uma página de jogo.</summary>
    public DetectedGame? Game { get; private set; }

    [ObservableProperty]
    private string _detectionText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand), nameof(RestoreBackupCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RunHint))]
    private string _runSummary = string.Empty;

    /// <summary>Aviso ao lado do resumo na barra de ação: o andamento da execução ou o lembrete do ponto de restauração.</summary>
    public string RunHint => string.IsNullOrEmpty(RunSummary) ? "  ·  ponto de restauração antes de executar" : $"  ·  {RunSummary}";

    public void SetDiagnostics(IEnumerable<DiagnosticItem> items)
    {
        Diagnostics.Clear();
        foreach (var item in items.Where(item => item.Area == _area))
            Diagnostics.Add(new DiagnosticItemViewModel(item));
    }

    /// <summary>Recarrega as ações disponíveis (somente leitura): locais de cache e pastas da Steam do jogo.</summary>
    public async Task LoadActionsAsync(DetectedGame? game, string? steamRoot)
    {
        Game = game;
        _steamRoot = steamRoot;
        Actions.Clear();
        if (_area == OptimizationArea.Windows)
        {
            foreach (var location in await _shaderCache.ScanAsync())
                AddAction(OptimizationActionViewModel.ForShaderCache(location));
        }
        if (game is { IsInstalled: true })
        {
            // A redefinição das configurações locais do CS2 (userdata) não é oferecida; backups antigos dela continuam restauráveis abaixo.
            var targets = await Task.Run(() => _steamFolders.GetTargets(game, steamRoot));
            foreach (var target in targets.Where(target => target.Kind != SteamFolderKind.UserConfig))
                AddAction(OptimizationActionViewModel.ForSteamFolder(target));
        }
        await LoadBackupsAsync();
        RunCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedActionsText));
    }

    private void AddAction(OptimizationActionViewModel action)
    {
        action.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OptimizationActionViewModel.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedActionsCount));
                OnPropertyChanged(nameof(SelectedActionsText));
                RunCommand.NotifyCanExecuteChanged();
            }
        };
        Actions.Add(action);
    }

    private async Task LoadBackupsAsync()
    {
        Backups.Clear();
        if (Game is null)
            return;
        foreach (var backup in await _steamFolders.GetBackupsAsync())
        {
            if (backup.GameId == Game.Game.Id)
                Backups.Add(new BackupItemViewModel(backup));
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunSelected))]
    private async Task RunAsync()
    {
        var selected = Actions.Where(action => action.IsSelected).ToArray();
        if (selected.Length == 0)
        {
            _dialogs.ShowInfo("Marque as ações que deseja executar.", Title);
            return;
        }

        // Falha cedo, antes do ponto de restauração, se a Steam ou o jogo estiverem abertos.
        if (selected.Select(action => action.SteamFolder?.Game).FirstOrDefault(game => game is not null) is { } steamGame &&
            _steamFolders.GetBlockingReason(steamGame) is { } blocking)
        {
            _dialogs.ShowError(blocking + "\n\nNada foi alterado.", Title);
            return;
        }

        var confirmation =
            "Serão executadas:\n\n" + string.Join("\n", selected.Select(action => $"• {action.Name} — risco {action.Risk}")) + "\n\n" +
            "• Antes de tudo, um ponto de restauração é criado (obrigatório).\n" +
            (selected.Any(action => action.ShaderCache is not null)
                ? "• Caches de shaders não têm backup: os jogos vão recompilar e podem travar por alguns instantes nas primeiras partidas.\n"
                : string.Empty) +
            "\nDeseja continuar?";
        if (!_dialogs.Confirm(confirmation, Title))
            return;
        if (!await _restorePointPrompt.EnsureAvailableAsync())
            return;

        IsBusy = true;
        try
        {
            RunSummary = "Criando o ponto de restauração obrigatório…";
            var restorePoint = await _restorePointGuard.CreateRestorePointAsync();
            if (!RestorePointGuard.IsRestorePointReady(restorePoint))
            {
                RunSummary = $"Bloqueado: o ponto de restauração não foi criado ({restorePoint.Message}). Nenhuma alteração foi feita.";
                foreach (var action in selected)
                    action.SetResult(restorePoint with { Outcome = "Bloqueado", Message = "Ponto de restauração não criado; nada foi alterado." });
                return;
            }

            var done = 0;
            foreach (var action in selected)
            {
                RunSummary = $"Executando {++done}/{selected.Length}: {action.Name}…";
                var result = action.ShaderCache is { } location
                    ? await _shaderCache.CleanAsync([location])
                    : await _steamFolders.BackupAndResetAsync(action.SteamFolder!);
                action.SetResult(result);
            }
            RunSummary = $"Concluído. Ponto de restauração: {restorePoint.Message}";
        }
        finally
        {
            IsBusy = false;
        }
        var results = selected.ToDictionary(action => action.Name, action => (action.ResultText, action.ResultBrush));
        await LoadActionsAsync(Game, _steamRoot);
        // Mantém o resultado visível nas ações que continuam disponíveis após a atualização.
        foreach (var action in Actions)
        {
            if (results.TryGetValue(action.Name, out var previous))
            {
                action.ResultText = previous.ResultText;
                action.ResultBrush = previous.ResultBrush;
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RestoreBackupAsync(BackupItemViewModel? item)
    {
        if (item is null)
            return;
        if (!_dialogs.Confirm(
                $"Restaurar \"{item.Label}\"?\n\nA pasta de backup volta ao lugar original. Se a Steam ou o jogo já recriaram a pasta, " +
                "a versão recriada é guardada ao lado (nada é apagado).\n\nA Steam e o jogo precisam estar fechados.",
                "Restaurar backup"))
            return;

        IsBusy = true;
        try
        {
            var result = await _steamFolders.RestoreAsync(item.Backup);
            RunSummary = $"{result.RequestedState}: {result.Outcome} — {result.Message}";
        }
        finally
        {
            IsBusy = false;
        }
        await LoadActionsAsync(Game, _steamRoot);
    }

    private bool CanRun() => !IsBusy;
    private bool CanRunSelected() => !IsBusy && SelectedActionsCount > 0;
}
