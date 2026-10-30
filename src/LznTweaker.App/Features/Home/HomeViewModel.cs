using CommunityToolkit.Mvvm.Input;
using LznTweaker.Infrastructure;

namespace LznTweaker.Features.Home;

public sealed record HomePrinciple(string Glyph, string Title, string Description);

/// <summary>Página de apresentação do LznTweaker: texto institucional e princípios. Não lê nem altera o sistema.</summary>
public sealed partial class HomeViewModel(INavigationService navigation)
{
    /// <summary>Versão do assembly (Version em Directory.Build.props).</summary>
    public string AppVersion => WindowsEnvironment.AppVersion;

    public IReadOnlyList<HomePrinciple> Principles { get; } =
    [
        new("", "Base técnica",
            "Ajustes fundamentados em documentação, testes e critérios técnicos sempre que disponíveis."),
        new("", "Segurança em primeiro lugar",
            "Atenção aos riscos, à compatibilidade e à possibilidade de reverter alterações quando tecnicamente viável."),
        new("", "Transparência",
            "Explicações claras sobre o que cada configuração faz, seus benefícios esperados e suas limitações.")
    ];

    [RelayCommand]
    private void Navigate(string? pageKey)
    {
        if (pageKey is not null)
            navigation.Navigate(pageKey);
    }
}
