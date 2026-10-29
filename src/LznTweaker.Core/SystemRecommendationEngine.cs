using System.IO;

namespace LznTweaker.Core;

/// <summary>Gera avisos explicáveis; nunca executa as ações descritas.</summary>
public static class SystemRecommendationEngine
{
    public static IReadOnlyList<SystemRecommendation> Analyze(SystemSnapshot snapshot)
    {
        var recommendations = new List<SystemRecommendation>();
        var systemDrive = snapshot.Drives.FirstOrDefault(drive =>
            string.Equals(drive.Name, Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));

        if (systemDrive is not null)
        {
            var freePercent = systemDrive.TotalBytes <= 0 ? 100 : systemDrive.FreeBytes * 100d / systemDrive.TotalBytes;
            if (freePercent < 10 || systemDrive.FreeBytes < 15L * 1_073_741_824)
            {
                recommendations.Add(new SystemRecommendation(
                    "Pouco espaço livre no volume do Windows",
                    $"O volume {systemDrive.Name} tem {systemDrive.FreeText} de {systemDrive.TotalText}. Espaço baixo pode dificultar atualizações e operações temporárias. Revise os arquivos manualmente; esta recomendação não remove nada.",
                    "Médio", false, "Armazenamento"));
            }
            else if (freePercent < 20)
            {
                recommendations.Add(new SystemRecommendation(
                    "Acompanhe o espaço do volume do Windows",
                    $"O volume {systemDrive.Name} está com {freePercent:0}% livre. Vale acompanhar o espaço antes de instalar jogos ou atualizações grandes.",
                    "Baixo", false, "Armazenamento"));
            }
        }

        if (snapshot.MemoryTotalBytes is > 0 and <= 8L * 1_073_741_824)
        {
            recommendations.Add(new SystemRecommendation(
                "Memória disponível pode limitar multitarefa",
                $"O sistema informa {snapshot.MemoryTotalBytes / 1_073_741_824d:0.#} GB de RAM. Fechar manualmente aplicativos que você reconhece antes de jogar pode reduzir a concorrência por memória; não desative serviços desconhecidos.",
                "Baixo", false, "Memória"));
        }

        if (snapshot.WindowsName.Contains("Windows 10", StringComparison.OrdinalIgnoreCase))
        {
            recommendations.Add(new SystemRecommendation(
                "Confira o suporte desta edição do Windows",
                "O suporte regular do Windows 10 terminou em 14 de outubro de 2025 para a maioria das edições. LTSC tem ciclo próprio, e dispositivos inscritos no ESU podem receber atualizações de segurança. Verifique a edição e a inscrição antes de decidir sobre atualização.",
                "Alto", false, "Compatibilidade"));
        }

        if (snapshot.CollectionWarnings.Count > 0)
        {
            recommendations.Add(new SystemRecommendation(
                "Inventário parcial",
                $"O Windows não disponibilizou dados de {string.Join(", ", snapshot.CollectionWarnings)} nesta leitura. Os valores ausentes não foram estimados; tente atualizar novamente.",
                "Informativo", false, "Diagnóstico"));
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add(new SystemRecommendation(
                "Nenhum alerta básico encontrado",
                "As verificações iniciais de espaço, memória e versão não encontraram um alerta nesta leitura. Isso não é um benchmark nem uma garantia de desempenho.",
                "Informativo", false));
        }

        return recommendations;
    }
}
