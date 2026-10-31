namespace LznTweaker.Core.Programs;

/// <summary>Filtros da página Programas.</summary>
public enum ProgramTag { Cpu, Memory, Latency, Benchmark, Diagnostics, Startup }

/// <summary>
/// Classificação informativa do que a ferramenta faz no sistema. Nenhum nível significa "sem risco":
/// mesmo uma ferramenta de consulta pode oferecer ações que alteram o Windows.
/// </summary>
public enum ProgramSafety
{
    /// <summary>Monitoramento e diagnóstico.</summary>
    ReadOnly,
    /// <summary>Modifica processos ou configurações do Windows.</summary>
    ControlledChange,
    /// <summary>Altera parâmetros de hardware, interrupções ou configurações sensíveis.</summary>
    Advanced
}

/// <summary>Licença declarada pelo próprio desenvolvedor; Unconfirmed quando a fonte não diz.</summary>
public enum ProgramLicense { Free, FreeWithPaidEdition, OpenSource, Unconfirmed }

/// <summary>De onde vem uma recomendação: documentação oficial, prática recorrente da comunidade ou experimento.</summary>
public enum EvidenceLevel { Documented, Community, Experimental }

/// <summary>Valor de referência (não universal) com o nível de evidência.</summary>
public sealed record ProgramSetting(string Name, string Value, EvidenceLevel Evidence, string? Note = null);

public sealed record ProgramSource(string Title, string Url, EvidenceLevel Kind);

/// <param name="Summary">O que faz, em uma ou duas frases.</param>
/// <param name="Purpose">Linha curta mostrada na lista.</param>
/// <param name="OfficialUrl">Página oficial (nunca o arquivo de instalação).</param>
/// <param name="IconResource">Nome do arquivo em Assets/Programs; nulo quando não há logotipo oficial publicado.</param>
/// <param name="Steps">Tutorial resumido.</param>
/// <param name="Cautions">Cuidados, incluindo as limitações.</param>
public sealed record ExternalProgram(
    string Id,
    string Name,
    string Developer,
    string Summary,
    string Purpose,
    IReadOnlyList<ProgramTag> Tags,
    ProgramSafety Safety,
    ProgramLicense License,
    string? LicenseNote,
    string OfficialUrl,
    string? IconResource,
    IReadOnlyList<ProgramSetting> Settings,
    IReadOnlyList<string> Steps,
    IReadOnlyList<string> Cautions,
    IReadOnlyList<ProgramSource> Sources);
