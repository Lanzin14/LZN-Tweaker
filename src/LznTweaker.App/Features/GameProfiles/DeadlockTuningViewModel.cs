using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LznTweaker.Core.GameTuning;
using LznTweaker.Infrastructure;

namespace LznTweaker.Features.GameProfiles;

public sealed partial class DeadlockProfileOption(GameConfigProfile profile) : ObservableObject
{
    private static readonly SolidColorBrush Green = Freeze("#6EE7B7");
    private static readonly SolidColorBrush Amber = Freeze("#F5C46B");
    private static readonly SolidColorBrush Red = Freeze("#F87171");

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isApplied;

    public GameConfigProfile Profile { get; } = profile;
    public string Name => Profile.Name;
    public string Summary => Profile.Summary;
    public string RiskText => $"Risco {Profile.RiskLevel}";
    public string? Warning => Profile.Warning;
    public bool HasWarning => Profile.Warning is not null;
    public Brush RiskBrush => Profile.RiskLevel switch { "Baixo" => Green, "Médio" => Amber, _ => Red };

    /// <summary>Texto do ícone de informação: o que muda, origem, risco e reversão.</summary>
    public string InfoTip =>
        $"{Profile.Details}\n\n" +
        (Profile.Warning is null ? string.Empty : $"Aviso: {Profile.Warning}\n\n") +
        $"O que é alterado: o arquivo game\\citadel\\gameinfo.gi do Deadlock é substituído pelo deste perfil (ConVars de vídeo, " +
        $"áudio, rede e interface). Nenhuma configuração do Windows é tocada.\n" +
        $"Autor: {Profile.Author} · Projeto OptimizationLock (GPL-3.0), baixado do GitHub na hora de aplicar.\n" +
        $"Reversão: o original é guardado antes e pode ser restaurado com \"Desfazer\".";

    private static SolidColorBrush Freeze(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}

public sealed partial class DeadlockTuningViewModel : ObservableObject
{
    private readonly GameTuningService _service;
    private readonly IUserDialogs _dialogs;
    private bool _initialized;

    public DeadlockTuningViewModel(GameTuningService service, IUserDialogs dialogs)
    {
        _service = service;
        _dialogs = dialogs;
        Profiles = new ObservableCollection<DeadlockProfileOption>(DeadlockProfiles.All.Select(profile => new DeadlockProfileOption(profile)));
        Profiles[0].IsSelected = true;
        foreach (var option in Profiles)
            option.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(DeadlockProfileOption.IsSelected))
                    ApplyCommand.NotifyCanExecuteChanged();
            };
    }

    public ObservableCollection<DeadlockProfileOption> Profiles { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInstallationFound))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand), nameof(UndoCommand))]
    private DeadlockInstallation? _installation;

    [ObservableProperty]
    private string _installationText = "Procurando o Deadlock nas bibliotecas da Steam…";

    [ObservableProperty]
    private string _statusText = "Estado ainda não consultado.";

    [ObservableProperty]
    private string _lastResultText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand), nameof(UndoCommand), nameof(BrowseCommand), nameof(DetectCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    private GameConfigState _state;

    public bool IsInstallationFound => Installation is not null;

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;
        _initialized = true;
        await DetectAsync();
    }

    [RelayCommand(CanExecute = nameof(CanChangeInstallation))]
    private async Task DetectAsync()
    {
        IsBusy = true;
        try
        {
            var found = await Task.Run(_service.LocateDeadlock);
            Installation = found;
            InstallationText = found is null
                ? "O Deadlock não foi encontrado nas bibliotecas da Steam. Escolha a pasta do jogo manualmente."
                : $"Encontrado pela Steam: {found.RootPath}";
            RefreshStatus();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanChangeInstallation))]
    private void Browse()
    {
        var folder = _dialogs.PickFolder("Selecione a pasta do Deadlock (steamapps\\common\\Deadlock)", Installation?.RootPath);
        if (folder is null)
            return;
        var selected = _service.FromUserSelection(folder, out var error);
        if (selected is null)
        {
            _dialogs.ShowError(error, "Pasta do Deadlock");
            return;
        }
        Installation = selected;
        InstallationText = $"Selecionado manualmente: {selected.RootPath}";
        RefreshStatus();
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        var option = Profiles.FirstOrDefault(profile => profile.IsSelected);
        if (Installation is not { } installation || option is null)
            return;

        IsBusy = true;
        try
        {
            LastResultText = $"Baixando \"{option.Name}\" do GitHub e validando o arquivo…";
            ProfileDownload download;
            try
            {
                download = await _service.DownloadAsync(option.Profile, installation);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.IO.InvalidDataException or TaskCanceledException)
            {
                LastResultText = $"Download não concluído: {ex.Message}";
                _dialogs.ShowError($"{ex.Message}\n\nNada foi alterado.", "Perfil do Deadlock");
                return;
            }

            if (!_dialogs.Confirm(BuildConfirmation(installation, download), $"Aplicar \"{option.Name}\""))
            {
                LastResultText = "Aplicação cancelada; nada foi alterado.";
                return;
            }

            var result = await _service.ApplyAsync(installation, download);
            LastResultText = $"{result.Outcome} — {result.Message}";
        }
        finally
        {
            IsBusy = false;
            RefreshStatus();
        }
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private async Task UndoAsync()
    {
        if (Installation is not { } installation)
            return;
        if (!_dialogs.Confirm(
                "O gameinfo.gi original será restaurado e o perfil do app será descartado.\n\n" +
                "Por segurança, a restauração só acontece se o arquivo atual ainda for exatamente o perfil aplicado; " +
                "se o Deadlock já o substituiu numa atualização, nada é sobrescrito.\n\nContinuar?",
                "Desfazer perfil do Deadlock"))
            return;

        IsBusy = true;
        try
        {
            var result = await _service.UndoAsync(installation);
            LastResultText = $"{result.Outcome} — {result.Message}";
        }
        finally
        {
            IsBusy = false;
            RefreshStatus();
        }
    }

    [RelayCommand]
    private void OpenSource(DeadlockProfileOption? option) =>
        _dialogs.OpenUrl(option?.Profile.WebUrl ?? DeadlockProfiles.RepositoryWebUrl);

    private bool CanChangeInstallation() => !IsBusy;
    private bool CanApply() => !IsBusy && Installation is not null && Profiles.Any(profile => profile.IsSelected);
    private bool CanUndo() => !IsBusy && Installation is not null && State != GameConfigState.NotApplied;

    private void RefreshStatus()
    {
        if (Installation is not { } installation)
        {
            State = GameConfigState.NotApplied;
            StatusText = "Selecione a pasta do jogo para ver o estado da configuração.";
            foreach (var option in Profiles)
                option.IsApplied = false;
            return;
        }

        try
        {
            var status = _service.GetStatus(installation);
            State = status.State;
            StatusText = status.Message;
            foreach (var option in Profiles)
                option.IsApplied = status.State == GameConfigState.Applied && option.Profile.Id == status.AppliedProfileId;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            StatusText = $"Não foi possível ler o gameinfo.gi: {ex.Message}";
        }
    }

    private static string BuildConfirmation(DeadlockInstallation installation, ProfileDownload download)
    {
        var profile = download.Profile;
        var version = download.CommitDate is { } date
            ? $"Versão do GitHub: {date.LocalDateTime:d} (commit {download.CommitSha?[..7]})."
            : "Data da versão no GitHub indisponível (limite da API ou sem conexão); o conteúdo foi validado mesmo assim.";
        var compatibility = download.CompatibilityWarnings.Count == 0
            ? "• Compatibilidade: o perfil carrega todos os caminhos de conteúdo do seu arquivo atual.\n"
            : "• ATENÇÃO, possível incompatibilidade com o patch atual:\n" + string.Join("\n", download.CompatibilityWarnings.Select(warning => $"   – {warning}")) + "\n";
        return
            $"Perfil: {profile.Name} (risco {profile.RiskLevel}, por {profile.Author})\n" +
            (profile.Warning is null ? string.Empty : $"Aviso do perfil: {profile.Warning}\n") +
            $"{version}\n\n" +
            $"• O arquivo {installation.GameInfoPath} será substituído.\n" +
            "• Antes disso, o original é guardado e conferido por hash; o \"Desfazer\" o restaura.\n" +
            compatibility +
            "• O Deadlock substitui este arquivo a cada atualização grande; depois de um patch, reaplique o perfil.\n" +
            "• O jogo exibe um aviso de que pode bloquear o matchmaking para quem tem ConVars alteradas no gameinfo.gi. Segundo o " +
            "autor do OptimizationLock, isso ainda não é aplicado, mas pode passar a ser.\n\n" +
            "Deseja aplicar?";
    }
}
