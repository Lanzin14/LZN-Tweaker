namespace LznTweaker.Core.Guides;

public enum GuideRecommendation { Disable, Enable, Check }

/// <summary>
/// Configuração de outro aplicativo que o usuário ajusta à mão. O LZN Tweaker só mostra o caminho e o passo a passo;
/// nada é lido nem alterado no aplicativo.
/// </summary>
/// <param name="Path">Etapas do caminho dentro do aplicativo, da tela inicial até a opção.</param>
/// <param name="Note">Avisos de versão: nome que pode variar, opção que mudou de lugar ou que pode não existir.</param>
public sealed record ManualSettingGuide(
    string Id,
    string Title,
    string Benefit,
    IReadOnlyList<string> Path,
    GuideRecommendation Recommendation,
    IReadOnlyList<string> Steps,
    string? Note,
    string SourceUrl);
