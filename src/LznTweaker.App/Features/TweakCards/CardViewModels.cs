using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LznTweaker.Core.Guides;
using LznTweaker.Core.Maintenance;
using LznTweaker.Core.Tweaks;

using LznTweaker.Infrastructure;

namespace LznTweaker.Features.TweakCards;

public enum RiskLevel { Low, Warning, High }

internal static class CardBrushes
{
    public static SolidColorBrush Low => ThemeBrushes.Success;
    public static SolidColorBrush Warning => ThemeBrushes.Warning;
    public static SolidColorBrush High => ThemeBrushes.Danger;
    // Estado é cor de estado: "Ativo" em verde, o roxo fica para ação e seleção.
    public static SolidColorBrush Active => ThemeBrushes.Success;
    public static SolidColorBrush Pending => ThemeBrushes.Warning;
    public static SolidColorBrush Muted => ThemeBrushes.Muted;

}

/// <summary>Base dos cards da grade: ícone, título, descrição, risco e estado.</summary>
public abstract partial class CardViewModel(string id, string tabKey, string glyph, string title, string description, RiskLevel risk) : ObservableObject
{
    public string Id { get; } = id;
    public string TabKey { get; } = tabKey;
    public string Glyph { get; } = glyph;
    public string Title { get; } = title;
    public string Description { get; } = description;
    public RiskLevel Risk { get; } = risk;
    public virtual bool IsRecommended => false;
    /// <summary>Grupo do card dentro da aba (ex.: Privacidade); nulo quando a aba não divide os cards.</summary>
    public CardCategoryInfo? Category { get; init; }

    public string RiskText => Risk switch { RiskLevel.Low => "Baixo risco", RiskLevel.Warning => "Atenção", _ => "Alto risco" };
    public string RiskGlyph => Risk switch { RiskLevel.Low => "", RiskLevel.Warning => "", _ => "" };
    public Brush RiskBrush => Risk switch { RiskLevel.Low => CardBrushes.Low, RiskLevel.Warning => CardBrushes.Warning, _ => CardBrushes.High };
    public abstract string RiskTooltip { get; }

    public abstract string StateText { get; }
    public abstract Brush StateBrush { get; }
    public abstract bool IsActive { get; }
    public abstract bool IsPending { get; }
    public abstract bool IsVerifiable { get; }

    /// <summary>Texto usado na busca instantânea.</summary>
    public virtual string SearchText => $"{Title} {Description}";

    public static RiskLevel FromCatalog(string riskLevel) => riskLevel switch
    {
        "Baixo" => RiskLevel.Low,
        "Médio" => RiskLevel.Warning,
        _ => RiskLevel.High
    };

    protected void NotifyState()
    {
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(StateBrush));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsPending));
    }
}

/// <summary>
/// Card de um ajuste do catálogo. O interruptor não altera o sistema sozinho: ligar marca "aplicar" e desligar marca
/// "desfazer"; tudo é revisado e executado em lote (com ponto de restauração). Desligar só é possível quando o app
/// guardou o estado anterior. Ajustes por script viram ação pontual (botão), porque não têm estado verificável.
/// </summary>
public sealed partial class TweakCardViewModel : CardViewModel
{
    private TweakSystemState _detectedState = TweakSystemState.Unknown;
    private string _detail = "Estado ainda não consultado.";
    private bool _hasUndo;
    private bool _suppress;

    public TweakCardViewModel(TweakDefinition definition, string tabKey, string glyph, bool isRecommended)
        : base(definition.Id, tabKey, glyph, definition.Name, definition.Description, FromCatalog(definition.RiskLevel))
    {
        Definition = definition;
        _isRecommended = isRecommended;
        IsToggle = definition.InvokeScript is null && !definition.IsOneShotOperation &&
                   (definition.RegistryChanges is { Count: > 0 } || definition.HklmRegistryChanges is { Count: > 0 } ||
                    definition.ServiceChanges is { Count: > 0 } || definition.ScheduledTasksToDisable is { Count: > 0 });
    }

    private readonly bool _isRecommended;
    public TweakDefinition Definition { get; }
    public bool IsToggle { get; }
    public bool IsAction => !IsToggle;
    public override bool IsRecommended => _isRecommended;

    [ObservableProperty]
    private bool _isOn;

    [ObservableProperty]
    private bool _isQueued;

    public TweakSystemState DetectedState => _detectedState;
    public bool HasUndo => _hasUndo;
    private bool ActualOn => _detectedState == TweakSystemState.Enabled;

    public bool PendingIsUndo => IsToggle && !IsOn && ActualOn;
    public override bool IsPending => IsToggle ? IsOn != ActualOn : IsQueued;
    public override bool IsActive => ActualOn;
    public override bool IsVerifiable => IsToggle && _detectedState is not TweakSystemState.Unknown;

    public bool IsSwitchEnabled => IsToggle && _detectedState is not (TweakSystemState.Unsupported or TweakSystemState.BlockedByPolicy) && (!ActualOn || _hasUndo);

    public string SwitchTooltip => _detectedState switch
    {
        TweakSystemState.Unsupported => "Indisponível nesta versão do Windows.",
        TweakSystemState.BlockedByPolicy => "Bloqueado por uma política do sistema.",
        TweakSystemState.Enabled when !_hasUndo => "Já está aplicado, mas o estado anterior não foi registrado pelo LznTweaker; não há como desfazer com segurança.",
        TweakSystemState.Enabled => "Desligar marca o ajuste para ser desfeito (restaura o valor anterior registrado).",
        _ => "Ligar marca o ajuste para ser aplicado. Nada muda até você revisar e aplicar."
    };

    public override string RiskTooltip => Risk switch
    {
        RiskLevel.Low => "Geralmente reversível e com impacto limitado.",
        RiskLevel.Warning => "Pode afetar recursos específicos ou o comportamento do sistema. Leia os detalhes.",
        _ => "Pode prejudicar funcionalidades, segurança, atualizações ou estabilidade. Leia os detalhes antes."
    } + (Definition.HasReliableRollback ? string.Empty : " Sem reversão automática pelo app.");

    public override string StateText => IsPending
        ? IsToggle ? (PendingIsUndo ? "Pendente · desfazer" : "Pendente · aplicar") : "Na fila · executar"
        : _detectedState switch
        {
            TweakSystemState.Enabled => "Ativo",
            TweakSystemState.Disabled => "Inativo",
            TweakSystemState.Mixed => "Parcial",
            TweakSystemState.Unsupported => "Indisponível",
            TweakSystemState.BlockedByPolicy => "Bloqueado por política",
            _ => IsToggle ? "Não verificado" : "Ação pontual"
        };

    public override Brush StateBrush => IsPending ? CardBrushes.Pending
        : _detectedState == TweakSystemState.Enabled ? CardBrushes.Active : CardBrushes.Muted;

    public string ActionButtonText => IsQueued ? "Na fila ✓" : "Adicionar à fila";
    public string RestartText => Definition.RestartNote ?? (Definition.RequiresRestart ? "Requer reinicialização" : "Aplica imediatamente");
    public string RollbackText => Definition.HasReliableRollback ? "Reversível pelo app (valor anterior registrado)" : "Sem reversão automática pelo app";
    public string AdministratorText => Definition.RequiresAdministrator ? "Exige administrador" : "Não exige administrador";
    public string Detail => _detail;
    public override string SearchText => $"{Title} {Description} {Definition.TechnicalReason}";

    /// <summary>Atualiza com o estado lido do sistema e descarta seleções pendentes.</summary>
    public void SetDetected(TweakSystemState state, string detail, bool hasUndo)
    {
        _detectedState = state;
        _detail = detail;
        _hasUndo = hasUndo;
        _suppress = true;
        IsOn = ActualOn;
        IsQueued = false;
        _suppress = false;
        OnPropertyChanged(nameof(DetectedState));
        OnPropertyChanged(nameof(HasUndo));
        OnPropertyChanged(nameof(IsSwitchEnabled));
        OnPropertyChanged(nameof(SwitchTooltip));
        OnPropertyChanged(nameof(Detail));
        OnPropertyChanged(nameof(IsVerifiable));
        NotifyState();
    }

    public void Discard()
    {
        _suppress = true;
        IsOn = ActualOn;
        IsQueued = false;
        _suppress = false;
        NotifyState();
    }

    public event Action<TweakCardViewModel>? PendingChanged;

    partial void OnIsOnChanged(bool value)
    {
        OnPropertyChanged(nameof(PendingIsUndo));
        NotifyState();
        if (!_suppress)
            PendingChanged?.Invoke(this);
    }

    partial void OnIsQueuedChanged(bool value)
    {
        OnPropertyChanged(nameof(ActionButtonText));
        NotifyState();
        if (!_suppress)
            PendingChanged?.Invoke(this);
    }
}

public sealed record QuickActionDefinition(
    string Id,
    string Glyph,
    string Title,
    string Description,
    RiskLevel Risk,
    string ButtonText,
    SystemTool? Tool = null,
    string? NavigateTo = null,
    string? Confirmation = null,
    string? Note = null);

/// <summary>Card de ação imediata (ferramenta do Windows ou atalho para outra página), com confirmação quando sensível.</summary>
public sealed partial class QuickActionCardViewModel(QuickActionDefinition definition, string tabKey)
    : CardViewModel(definition.Id, tabKey, definition.Glyph, definition.Title, definition.Description, definition.Risk)
{
    public QuickActionDefinition Definition { get; } = definition;
    public string ButtonText => Definition.ButtonText;
    public string? Note => Definition.Note;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText), nameof(StateBrush))]
    private string? _lastResult;

    [ObservableProperty]
    private bool _isRunning;

    public override string RiskTooltip => Risk switch
    {
        RiskLevel.Low => "Ferramenta de diagnóstico ou atalho; não altera configurações.",
        RiskLevel.Warning => "Altera o sistema; leia a confirmação antes.",
        _ => "Pode afetar o funcionamento do sistema."
    };

    public override string StateText => LastResult ?? (Definition.NavigateTo is null ? "Pronto para executar" : "Atalho");
    public override Brush StateBrush => CardBrushes.Muted;
    public override bool IsActive => false;
    public override bool IsPending => false;
    public override bool IsVerifiable => false;
}

/// <summary>
/// Configuração manual em outro aplicativo (ex.: Discord): caminho, estado recomendado e passo a passo. O app não lê
/// nem altera nada; o estado é sempre "Requer configuração manual".
/// </summary>
public sealed class GuideCardViewModel(ManualSettingGuide guide, string tabKey)
    : CardViewModel(guide.Id, tabKey, string.Empty, guide.Title, guide.Benefit, RiskLevel.Low)
{
    public ManualSettingGuide Guide { get; } = guide;
    public string PathText => string.Join("  ›  ", Guide.Path);
    public IReadOnlyList<string> NumberedSteps { get; } = guide.Steps.Select((step, index) => $"{index + 1}. {step}").ToArray();
    public string? Note => Guide.Note;

    public string RecommendationText => Guide.Recommendation switch
    {
        GuideRecommendation.Disable => "Recomendado: Desativar",
        GuideRecommendation.Enable => "Recomendado: Ativar",
        _ => "Recomendado: Verificar"
    };

    public string RecommendationGlyph => Guide.Recommendation switch
    {
        GuideRecommendation.Disable => "",
        GuideRecommendation.Enable => "",
        _ => ""
    };

    public Brush RecommendationBrush => Guide.Recommendation == GuideRecommendation.Check ? CardBrushes.Warning : CardBrushes.Low;

    public override string RiskTooltip => "Configuração feita por você dentro do aplicativo; o LZN Tweaker não altera nada.";
    public override string StateText => "Requer configuração manual";
    public override Brush StateBrush => CardBrushes.Muted;
    public override bool IsActive => false;
    public override bool IsPending => false;
    public override bool IsVerifiable => false;
    public override string SearchText => $"{Title} {Description} {PathText} Discord";
}

/// <summary>Cabeçalho de um grupo de cards. Uma instância por categoria: o agrupamento da lista compara por referência.</summary>
public sealed class CardCategoryInfo(string title, string glyph, string description, int order)
{
    public string Title { get; } = title;
    public string Glyph { get; } = glyph;
    public string Description { get; } = description;
    public int Order { get; } = order;
}

public sealed partial class CardTabViewModel(string key, string title, string glyph, string description = "", string? highlight = null) : ObservableObject
{
    public string Key { get; } = key;
    public string Title { get; } = title;
    public string Glyph { get; } = glyph;
    /// <summary>Frase curta sobre o conteúdo da aba; vazia quando a página já explica.</summary>
    public string Description { get; } = description;
    /// <summary>Aviso em destaque acima dos cards (ex.: "IMPORTANTE SE VOCÊ USA DISCORD.").</summary>
    public string? Highlight { get; } = highlight;

    [ObservableProperty]
    private bool _isSelected;
}
