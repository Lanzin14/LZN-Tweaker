using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LznTweaker.Core.Debloat;
using LznTweaker.Infrastructure;

namespace LznTweaker.Features.Debloat;

public sealed class AppxItemViewModel(AppxPackageInfo package)
{
    private static readonly SolidColorBrush Green = Freeze("#6EE7B7");
    private static readonly SolidColorBrush Amber = Freeze("#F5C46B");
    private static readonly SolidColorBrush Purple = Freeze("#DDD0FF");

    public AppxPackageInfo Package { get; } = package;
    public string FriendlyName => Package.FriendlyName;
    public string Name => Package.Name;
    public AppxCategory Category => Package.Category;
    public bool IsSystem => Package.Category == AppxCategory.System;

    /// <summary>Ordem e título dos grupos na lista da esquerda.</summary>
    public string CategoryDisplay => Package.Category switch
    {
        AppxCategory.Sponsored => "1 · Patrocinados e pré-instalados (seguros para remover)",
        AppxCategory.System => "2 · Apps do sistema (podem afetar funções do Windows)",
        _ => "3 · Instalados por você ou por terceiros"
    };

    public string BadgeText => Package.Category switch
    {
        AppxCategory.Sponsored => "Patrocinado",
        AppxCategory.System => "Sistema",
        _ => "Seu app"
    };

    public Brush BadgeBrush => Package.Category switch
    {
        AppxCategory.Sponsored => Green,
        AppxCategory.System => Amber,
        _ => Purple
    };

    public string InfoTip =>
        $"{Package.FriendlyName}\nPacote: {Package.Name} · versão {Package.Version}\n\n" +
        Package.Category switch
        {
            AppxCategory.Sponsored => "App promocional ou pré-instalado da lista curada (base: preset AppxDefault do WinUtil). Seguro para remover.",
            AppxCategory.System => "App da Microsoft que faz parte da experiência do Windows. A remoção pode afetar funcionalidades do Windows " +
                                   "ou de outros apps que dependem dele.",
            _ => "App que não está na lista curada; provavelmente foi instalado por você. Remova só se tiver certeza."
        } +
        (Package.Note is null ? string.Empty : $"\n\nObservação: {Package.Note}") +
        (Package.IsProvisioned == true ? "\n\nEstá provisionado para novas contas: os arquivos tendem a continuar no disco, então a restauração local costuma funcionar." : string.Empty);

    private static SolidColorBrush Freeze(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}

public sealed partial class RemovedAppxItemViewModel(RemovedAppxPackage package) : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected = true;

    public RemovedAppxPackage Package { get; } = package;
    public string FriendlyName => Package.FriendlyName;
    public string Details =>
        $"Removido em {Package.RemovedAt.LocalDateTime:g}{(Package.RemovedForAllUsers ? " para todas as contas" : string.Empty)} · " +
        (Package.FilesKeptAfterRemoval ? "restauração local provável" : "volta pela Microsoft Store");
}

public sealed partial class AppDebloatViewModel : ObservableObject
{
    private readonly AppxDebloatService _service;
    private readonly RestorePointPrompt _restorePointPrompt;
    private readonly IUserDialogs _dialogs;
    private bool _initialized;

    public AppDebloatViewModel(AppxDebloatService service, RestorePointPrompt restorePointPrompt, IUserDialogs dialogs)
    {
        _service = service;
        _restorePointPrompt = restorePointPrompt;
        _dialogs = dialogs;
        InstalledView = CollectionViewSource.GetDefaultView(Installed);
        InstalledView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(AppxItemViewModel.CategoryDisplay)));
        InstalledView.SortDescriptions.Add(new SortDescription(nameof(AppxItemViewModel.CategoryDisplay), ListSortDirection.Ascending));
        InstalledView.SortDescriptions.Add(new SortDescription(nameof(AppxItemViewModel.FriendlyName), ListSortDirection.Ascending));
        InstalledView.Filter = item => item is AppxItemViewModel app &&
            (string.IsNullOrWhiteSpace(SearchText) ||
             app.FriendlyName.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
             app.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        Queue.CollectionChanged += (_, _) =>
        {
            RemoveSelectedCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(QueueSummary));
        };
    }

    public ObservableCollection<AppxItemViewModel> Installed { get; } = [];
    public ICollectionView InstalledView { get; }
    public ObservableCollection<AppxItemViewModel> Queue { get; } = [];
    public ObservableCollection<RemovedAppxItemViewModel> Removed { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand), nameof(RemoveSelectedCommand), nameof(RestoreCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Carregando aplicativos instalados…";

    [ObservableProperty]
    private string? _accountWarning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QueueSummary))]
    private bool _removeForAllUsers;

    public string QueueSummary
    {
        get
        {
            var system = Queue.Count(item => item.IsSystem);
            return Queue.Count == 0
                ? "Nenhum app selecionado."
                : $"{Queue.Count} app(s) na fila{(system > 0 ? $" · {system} do sistema" : string.Empty)}" +
                  (RemoveForAllUsers ? " · para TODAS as contas (volta só pela Store)" : " · somente a conta atual");
        }
    }

    partial void OnSearchTextChanged(string value) => InstalledView.Refresh();

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;
        _initialized = true;
        await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            StatusText = "Consultando os pacotes Appx do Windows…";
            var inventory = await _service.GetInventoryAsync();
            Installed.Clear();
            Queue.Clear();
            foreach (var package in inventory.Packages)
                Installed.Add(new AppxItemViewModel(package));
            AccountWarning = inventory.AccountWarning;
            var sponsored = inventory.Packages.Count(package => package.Category == AppxCategory.Sponsored);
            StatusText = $"{inventory.Packages.Count} app(s) removíveis encontrados ({sponsored} patrocinados). " +
                         $"{inventory.ProtectedCount} pacote(s) do sistema, frameworks e componentes de outros programas foram ocultados por segurança.";
            await LoadRemovedAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.IO.IOException or System.Text.Json.JsonException)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddToQueue(IList? selected) => Move(selected?.OfType<AppxItemViewModel>().ToArray() ?? [], Installed, Queue);

    [RelayCommand]
    private void RemoveFromQueue(IList? selected) => Move(selected?.OfType<AppxItemViewModel>().ToArray() ?? [], Queue, Installed);

    [RelayCommand]
    private void AddAllSponsored() => Move(Installed.Where(item => item.Category == AppxCategory.Sponsored).ToArray(), Installed, Queue);

    [RelayCommand]
    private void ClearQueue() => Move(Queue.ToArray(), Queue, Installed);

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private async Task RemoveSelectedAsync()
    {
        var apps = Queue.ToArray();
        if (!await _restorePointPrompt.EnsureAvailableAsync())
            return;

        var system = apps.Where(app => app.IsSystem).ToArray();
        var message =
            "Os aplicativos abaixo serão removidos em um único lote:\n\n" +
            string.Join("\n", apps.Select(app => $"• {app.FriendlyName}{(app.IsSystem ? "  ⚠ sistema" : string.Empty)}")) + "\n\n" +
            "• Antes de tudo, um ponto de restauração é criado (obrigatório). Ele protege o resto do sistema, mas não traz apps da Store de volta.\n" +
            "• Cada remoção é conferida e registrada no histórico; \"Restaurar apps removidos\" re-registra o app quando os arquivos ainda existem " +
            "ou abre a página dele na Microsoft Store.\n" +
            (system.Length > 0 ? $"• {system.Length} app(s) do sistema: a remoção pode afetar funcionalidades do Windows.\n" : string.Empty) +
            (RemoveForAllUsers
                ? "• Remoção para TODAS as contas, incluindo novas: o Windows apaga os arquivos e a volta só é possível pela Microsoft Store.\n"
                : "• Somente a conta atual é afetada.\n") +
            "\nDeseja continuar?";
        if (!_dialogs.Confirm(message, "Remover aplicativos"))
            return;
        if (RemoveForAllUsers && !_dialogs.Confirm(
                "Confirmação extra: remover para todas as contas não pode ser desfeito localmente.\n\n" +
                "Os apps só poderão voltar reinstalando pela Microsoft Store, conta por conta. Continuar mesmo assim?",
                "Remover para todos os usuários"))
            return;

        IsBusy = true;
        try
        {
            var progress = new Progress<AppxBatchProgress>(update =>
                StatusText = $"Progresso {update.Completed}/{update.Total} · {update.Name}: {update.Outcome}");
            var results = await _service.RemoveAsync(apps.Select(app => app.Package).ToArray(), RemoveForAllUsers, progress);
            var removed = results.Count(result => result.TweakId.StartsWith(AppxDebloatService.OperationPrefix, StringComparison.Ordinal) && result.Outcome == "Sucesso verificado");
            var failures = results.Where(result => result.Outcome is not ("Sucesso verificado" or "Nada a alterar" or "Ponto recente reutilizado")).ToArray();
            StatusText = $"{removed} de {apps.Length} app(s) removido(s)." +
                         (failures.Length == 0 ? string.Empty : " Problemas: " + string.Join(" | ", failures.Select(result => $"{result.RequestedState}: {result.Outcome} — {result.Message}")));
        }
        finally
        {
            IsBusy = false;
        }
        await RefreshAfterBatchAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private async Task RestoreAsync()
    {
        var selected = Removed.Where(item => item.IsSelected).ToArray();
        if (selected.Length == 0)
            return;
        if (!_dialogs.Confirm(
                "Os aplicativos abaixo serão restaurados:\n\n" + string.Join("\n", selected.Select(item => $"• {item.FriendlyName} — {item.Details}")) +
                "\n\nQuando os arquivos ainda existem, o app é re-registrado a partir do manifesto local. Quando não existem, " +
                "o LznTweaker oferece abrir a página do app na Microsoft Store.\n\nContinuar?",
                "Restaurar aplicativos"))
            return;

        IsBusy = true;
        IReadOnlyList<Core.Tweaks.TweakOperationResult> results;
        try
        {
            results = await _service.RestoreAsync(selected.Select(item => item.Package).ToArray());
        }
        finally
        {
            IsBusy = false;
        }

        var restored = results.Count(result => result.Outcome is "Reversão verificada" or "Nada a alterar");
        var storeOnly = selected.Where(item => results.Any(result =>
            result.TweakId == AppxDebloatService.OperationPrefix + item.Package.Name && result.Outcome == "Requer Microsoft Store")).ToArray();
        var failures = results.Where(result => result.Outcome is not ("Reversão verificada" or "Nada a alterar" or "Requer Microsoft Store")).ToArray();
        StatusText = $"{restored} app(s) restaurado(s)." +
                     (storeOnly.Length > 0 ? $" {storeOnly.Length} só podem voltar pela Microsoft Store." : string.Empty) +
                     (failures.Length > 0 ? " Problemas: " + string.Join(" | ", failures.Select(result => $"{result.RequestedState}: {result.Message}")) : string.Empty);

        if (storeOnly.Length > 0 && _dialogs.Confirm(
                "Estes aplicativos não têm mais arquivos no disco e precisam ser reinstalados pela Microsoft Store:\n\n" +
                string.Join("\n", storeOnly.Select(item => $"• {item.FriendlyName}")) +
                "\n\nAbrir a página de cada um na Store agora? Depois de instalar, clique em Atualizar para tirá-los desta lista.",
                "Reinstalar pela Microsoft Store"))
        {
            foreach (var item in storeOnly)
                _dialogs.OpenStorePage(item.Package.StoreUri);
        }
        await RefreshAfterBatchAsync();
    }

    private bool CanRun() => !IsBusy;
    private bool CanRemove() => !IsBusy && Queue.Count > 0;
    private bool CanRestore() => !IsBusy && Removed.Count > 0;

    private async Task RefreshAfterBatchAsync()
    {
        var status = StatusText;
        await RefreshAsync();
        StatusText = status;
    }

    private async Task LoadRemovedAsync()
    {
        Removed.Clear();
        // Um app reinstalado (por exemplo, pela Store) volta a aparecer no inventário e sai desta lista.
        var installedNames = Installed.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var package in await _service.GetRestorableAsync())
        {
            if (!installedNames.Contains(package.Name))
                Removed.Add(new RemovedAppxItemViewModel(package));
        }
        RestoreCommand.NotifyCanExecuteChanged();
    }

    private static void Move(IReadOnlyList<AppxItemViewModel> items, ObservableCollection<AppxItemViewModel> from, ObservableCollection<AppxItemViewModel> to)
    {
        foreach (var item in items)
        {
            if (from.Remove(item))
                to.Add(item);
        }
    }
}
