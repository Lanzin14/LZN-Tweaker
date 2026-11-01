using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LznTweaker.Features.TweakCards;
using LznTweaker.Shell;

namespace LznTweaker.Features.Tools;

/// <summary>Ferramenta numa linha: o botão executa direto (com a confirmação da própria ferramenta, quando houver).</summary>
public sealed record ToolRow(QuickActionCardViewModel Card, ICommand Run);

/// <summary>Grupo de ferramentas com título e uma frase.</summary>
public sealed record ToolGroup(string Title, string Description, IReadOnlyList<ToolRow> Rows);

public sealed record ToolsTab(string Key, string Title);

/// <summary>
/// Ferramentas: Reparos, Rede, Hardware e Programas numa página com abas (docs/REESTRUTURACAO-UX.md, fase 3).
/// Reparos e Rede reaproveitam as ferramentas e o registro no histórico das páginas de cards; Hardware e Programas
/// são as páginas existentes, embutidas sem o próprio cabeçalho.
/// </summary>
public sealed partial class ToolsViewModel : ObservableObject
{
    public ToolsViewModel(FixesPageViewModel fixes, NetworkPageViewModel network)
    {
        Repairs = Groups(fixes);
        Network = Groups(network);
    }

    public IReadOnlyList<ToolsTab> Tabs { get; } =
    [
        new(ShellPages.Fixes, "Reparos"),
        new(ShellPages.Network, "Rede"),
        new(ShellPages.Hardware, "Hardware"),
        new(ShellPages.Programs, "Programas")
    ];

    public IReadOnlyList<ToolGroup> Repairs { get; }
    public IReadOnlyList<ToolGroup> Network { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRepairs), nameof(IsNetwork), nameof(IsHardware), nameof(IsPrograms), nameof(TabNote))]
    private string _selectedTabKey = ShellPages.Fixes;

    [RelayCommand]
    private void SelectTab(string? key)
    {
        if (key is not null)
            SelectedTabKey = key;
    }

    public bool IsRepairs => SelectedTabKey == ShellPages.Fixes;
    public bool IsNetwork => SelectedTabKey == ShellPages.Network;
    public bool IsHardware => SelectedTabKey == ShellPages.Hardware;
    public bool IsPrograms => SelectedTabKey == ShellPages.Programs;

    /// <summary>Uma linha sob as abas com o que vale para a aba inteira (em vez de repetir em cada item).</summary>
    public string TabNote => SelectedTabKey switch
    {
        ShellPages.Fixes => "Ferramentas do próprio Windows. Abrem numa janela visível e cada execução fica no Histórico.",
        ShellPages.Network => "Diagnóstico só lê a conexão. Os ajustes de IPv4/IPv6 ficam em Ajustes do Windows › Rede.",
        ShellPages.Hardware => "Somente consulta: o app nunca altera BIOS, XMP, overclock ou undervolt.",
        _ => "Todos são opcionais. O LZN Tweaker não baixa, instala nem executa estes programas. Meça antes e depois."
    };

    private static IReadOnlyList<ToolGroup> Groups(TweakCardsViewModel page) =>
        page.Tabs.Select(tab => new ToolGroup(tab.Title, tab.Description,
                page.Cards.OfType<QuickActionCardViewModel>().Where(card => card.TabKey == tab.Key)
                    .Select(card => new ToolRow(card, page.RunQuickActionCommand)).ToArray()))
            .Where(group => group.Rows.Count > 0)
            .ToArray();
}
