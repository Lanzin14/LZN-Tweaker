using System.IO;
using LznTweaker.Core.Diagnostics;

namespace LznTweaker.Infrastructure;

public enum ProblemKind { Info, Warning, Error }

/// <summary>
/// Problema mostrado ao usuário: título e orientação em linguagem simples; o texto técnico original fica em
/// <see cref="Details"/>, recolhido em "Detalhes técnicos" (ver Controls/Banner).
/// </summary>
public sealed record ProblemInfo(ProblemKind Kind, string Title, string Message, string? Details = null)
{
    /// <summary>
    /// Traduz uma falha de leitura em orientação. <paramref name="whatFailed"/> descreve o que não pôde ser feito
    /// (ex.: "Não foi possível listar os aplicativos"). A falha é registrada no AppLog.
    /// </summary>
    public static ProblemInfo FromException(Exception ex, string whatFailed)
    {
        AppLog.Error(whatFailed, ex);
        if (NeedsAdministrator(ex))
            return new(ProblemKind.Warning, "O LZN Tweaker precisa de permissão de administrador",
                "Esta leitura só funciona com o app aberto como administrador. Feche o LZN Tweaker e abra de novo, aprovando o pedido do Windows. Nada foi alterado.",
                ex.Message);
        return ex switch
        {
            IOException => new(ProblemKind.Error, whatFailed,
                "Um arquivo necessário está em uso ou inacessível. Feche programas que possam estar usando-o e tente de novo. Nada foi alterado.", ex.Message),
            System.Text.Json.JsonException => new(ProblemKind.Error, whatFailed,
                "Um arquivo de dados do app está corrompido ou incompleto. Tente de novo; se continuar, os detalhes abaixo ajudam a identificar qual. Nada foi alterado.", ex.Message),
            _ => new(ProblemKind.Error, whatFailed, "Tente de novo com Atualizar. Se continuar, os detalhes abaixo ajudam a identificar o problema. Nada foi alterado.", ex.Message)
        };
    }

    private static bool NeedsAdministrator(Exception ex) =>
        ex is UnauthorizedAccessException ||
        ex.Message.Contains("administrador", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("administrator", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("elevat", StringComparison.OrdinalIgnoreCase);
}
