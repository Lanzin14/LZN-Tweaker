using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LznTweaker.Core.Programs;
using LznTweaker.Infrastructure;

namespace LznTweaker.Features.Programs;

public sealed partial class ProgramFilterOption(ProgramTag? tag, string title) : ObservableObject
{
    public ProgramTag? Tag { get; } = tag;
    public string Title { get; } = title;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>Valor de referência numa linha só; a origem e a observação ficam na dica (ToolTip).</summary>
public sealed record ProgramSettingRow(string Name, string Value, string? Note, string EvidenceText, Brush EvidenceBrush)
{
    public string Tooltip => Note is null ? EvidenceText : $"{EvidenceText}. {Note}";
}

public sealed record ProgramSourceRow(string Title, string Url, string KindText);

/// <summary>Número do passo do tutorial a partir do AlternationIndex (começa em 0).</summary>
public sealed class StepNumberConverter : System.Windows.Data.IValueConverter
{
    public static StepNumberConverter Instance { get; } = new();
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        value is int index ? (index + 1).ToString(culture) : string.Empty;
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Card de um programa externo: informações, configurações de referência e fontes.</summary>
public sealed partial class ProgramCardViewModel : ObservableObject
{
    public ProgramCardViewModel(ExternalProgram program)
    {
        Program = program;
        Icon = LoadIcon(program.IconResource);
        Settings = program.Settings.Select(setting =>
        {
            var (text, brush) = Evidence(setting.Evidence);
            return new ProgramSettingRow(setting.Name, setting.Value, setting.Note, text, brush);
        }).ToArray();
        Sources = program.Sources.Select(source => new ProgramSourceRow(source.Title, source.Url, Evidence(source.Kind).Text)).ToArray();
    }

    public ExternalProgram Program { get; }
    public string Name => Program.Name;
    public string Developer => Program.Developer;
    public string Summary => Program.Summary;
    public string Purpose => Program.Purpose;
    public IReadOnlyList<string> TagTexts => Program.Tags.Select(TagText).ToArray();
    public IReadOnlyList<string> Steps => Program.Steps;
    public IReadOnlyList<string> Cautions => Program.Cautions;
    public string OfficialHost => Uri.TryCreate(Program.OfficialUrl, UriKind.Absolute, out var uri) ? uri.Host : Program.OfficialUrl;
    public IReadOnlyList<ProgramSettingRow> Settings { get; }
    public IReadOnlyList<ProgramSourceRow> Sources { get; }
    public bool HasSettings => Settings.Count > 0;
    public bool HasSteps => Steps.Count > 0;
    public string SourcesTitle => $"Fontes e referências ({Sources.Count})";
    public ImageSource? Icon { get; }
    public bool HasIcon => Icon is not null;
    public string Monogram => new string(Program.Name.Where(char.IsLetterOrDigit).Take(2).ToArray()).ToUpperInvariant();

    public string SafetyText => Program.Safety switch
    {
        ProgramSafety.ReadOnly => "Seguro para consulta",
        ProgramSafety.ControlledChange => "Alteração controlada",
        _ => "Avançado"
    };

    public string SafetyTooltip => Program.Safety switch
    {
        ProgramSafety.ReadOnly => "Ferramenta de monitoramento e diagnóstico. Algumas oferecem ações que alteram o sistema: leia antes de usar.",
        ProgramSafety.ControlledChange => "Modifica processos ou configurações do Windows. Anote o estado anterior e teste uma mudança por vez.",
        _ => "Altera parâmetros de hardware, interrupções ou configurações sensíveis. Use só com diagnóstico e backup."
    };

    public Brush SafetyBrush => Program.Safety switch
    {
        ProgramSafety.ReadOnly => ThemeBrushes.Success,
        ProgramSafety.ControlledChange => ThemeBrushes.Warning,
        _ => ThemeBrushes.Danger
    };

    public string LicenseText => Program.License switch
    {
        ProgramLicense.Free => "Gratuito",
        ProgramLicense.FreeWithPaidEdition => "Gratuito · versão paga opcional",
        ProgramLicense.OpenSource => "Gratuito · código aberto",
        _ => "Licença: consulte o site"
    };

    [ObservableProperty]
    private bool _isExpanded;

    public bool Matches(ProgramTag? tag, string search)
    {
        if (tag is { } wanted && !Program.Tags.Contains(wanted))
            return false;
        if (string.IsNullOrWhiteSpace(search))
            return true;
        var text = search.Trim();
        return Program.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase)
            || Program.Developer.Contains(text, StringComparison.CurrentCultureIgnoreCase);
    }

    public static string TagText(ProgramTag tag) => tag switch
    {
        ProgramTag.Cpu => "CPU",
        ProgramTag.Memory => "RAM",
        ProgramTag.Latency => "Latência",
        ProgramTag.Benchmark => "Benchmark",
        ProgramTag.Diagnostics => "Diagnóstico",
        _ => "Inicialização"
    };

    private static (string Text, Brush Brush) Evidence(EvidenceLevel level) => level switch
    {
        EvidenceLevel.Documented => ("Documentado oficialmente", ThemeBrushes.Success),
        EvidenceLevel.Community => ("Recorrente na comunidade", ThemeBrushes.AccentText),
        _ => ("Experimental: teste no seu PC", ThemeBrushes.Warning)
    };

    /// <summary>Logotipo embutido no app (Assets/Programs); sem arquivo, a interface usa o monograma.</summary>
    private static ImageSource? LoadIcon(string? resource)
    {
        if (resource is null)
            return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri($"pack://application:,,,/LznTweaker;component/Assets/Programs/{resource}");
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is System.IO.IOException or NotSupportedException or UriFormatException)
        {
            return null;
        }
    }
}

/// <summary>
/// Programas recomendados: catálogo de ferramentas externas, só com links para páginas oficiais. O app não baixa,
/// instala nem executa nenhuma delas; a ausência de um programa não afeta nenhum módulo do LZN Tweaker.
/// </summary>
public sealed partial class ProgramsViewModel : ObservableObject
{
    private readonly IUserDialogs _dialogs;

    public ProgramsViewModel(IUserDialogs dialogs)
    {
        _dialogs = dialogs;
        Programs = new(ProgramCatalog.All.Select(program => new ProgramCardViewModel(program)));
        Filters =
        [
            new(null, "Todos"),
            new(ProgramTag.Cpu, "CPU"),
            new(ProgramTag.Memory, "Memória RAM"),
            new(ProgramTag.Latency, "Latência"),
            new(ProgramTag.Benchmark, "Benchmark"),
            new(ProgramTag.Diagnostics, "Diagnóstico"),
            new(ProgramTag.Startup, "Inicialização")
        ];
        foreach (var filter in Filters)
            filter.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName != nameof(ProgramFilterOption.IsSelected) || sender is not ProgramFilterOption { IsSelected: true } selected)
                    return;
                // O filtro anterior ainda está marcado neste momento; o ativo é guardado aqui e os demais são desmarcados.
                _selectedTag = selected.Tag;
                foreach (var other in Filters.Where(other => other != selected))
                    other.IsSelected = false;
                RefreshView();
            };
        ProgramsView = CollectionViewSource.GetDefaultView(Programs);
        ProgramsView.Filter = item => item is ProgramCardViewModel card && card.Matches(SelectedTag, SearchText);
        Filters[0].IsSelected = true;
    }

    public ObservableCollection<ProgramCardViewModel> Programs { get; }
    public ICollectionView ProgramsView { get; }
    public IReadOnlyList<ProgramFilterOption> Filters { get; }
    public int VisibleCount => ProgramsView.Cast<object>().Count();
    public string VisibleCountText => Plural.Of(VisibleCount, "programa", "programas");
    private ProgramTag? _selectedTag;
    private ProgramTag? SelectedTag => _selectedTag;

    [ObservableProperty]
    private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value) => RefreshView();

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        Filters[0].IsSelected = true;
    }

    [RelayCommand]
    private void ToggleDetails(ProgramCardViewModel? card)
    {
        if (card is not null)
            card.IsExpanded = !card.IsExpanded;
    }

    /// <summary>Abre a página oficial no navegador padrão (só https; nunca um instalador).</summary>
    [RelayCommand]
    private void OpenSite(ProgramCardViewModel? card)
    {
        if (card is not null)
            _dialogs.OpenUrl(card.Program.OfficialUrl);
    }

    [RelayCommand]
    private void OpenSource(ProgramSourceRow? source)
    {
        if (source is not null)
            _dialogs.OpenUrl(source.Url);
    }

    private void RefreshView()
    {
        ProgramsView.Refresh();
        OnPropertyChanged(nameof(VisibleCount));
        OnPropertyChanged(nameof(VisibleCountText));
        // O detalhe acompanha a lista: se o programa selecionado saiu do filtro, mostra o primeiro visível.
        if (SelectedProgram is null || !SelectedProgram.Matches(SelectedTag, SearchText))
            SelectedProgram = ProgramsView.Cast<ProgramCardViewModel>().FirstOrDefault();
    }

    /// <summary>Programa mostrado no detalhe.</summary>
    [ObservableProperty]
    private ProgramCardViewModel? _selectedProgram;

    [RelayCommand]
    private void ClearSelection() => SelectedProgram = null;
}
