using System.Globalization;
using System.IO;
using System.Text;

namespace LznTweaker.Core.Diagnostics;

/// <summary>
/// Log em arquivo de texto, um por dia, em %LOCALAPPDATA%\LznTweaker\logs (os 14 mais recentes são mantidos).
/// Registra falhas e eventos de execução para diagnóstico; nunca lança exceção nem grava senhas ou conteúdo de arquivos.
/// </summary>
public static class AppLog
{
    private const int KeepFiles = 14;
    private static readonly object Gate = new();
    private static string _directory = DefaultDirectory;

    // Propriedade calculada: um inicializador estático aqui rodaria depois de _directory e o deixaria nulo.
    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LznTweaker", "logs");

    public static string Directory => _directory;

    /// <summary>Troca a pasta do log (usado pelos testes) e remove arquivos antigos.</summary>
    public static void Initialize(string? directory = null)
    {
        lock (Gate)
            _directory = directory ?? DefaultDirectory;
        Prune();
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message, Exception? exception = null) => Write("WARN", message, exception);

    public static void Error(string message, Exception? exception = null) => Write("ERRO", message, exception);

    internal static string CurrentFile => Path.Combine(_directory, $"lzn-{DateTime.Now:yyyyMMdd}.log");

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            var line = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
                .Append(' ').Append(level).Append(' ').Append(message.ReplaceLineEndings(" | "));
            if (exception is not null)
                line.AppendLine().Append("    ").Append(exception.ToString().ReplaceLineEndings(Environment.NewLine + "    "));
            line.AppendLine();
            lock (Gate)
            {
                System.IO.Directory.CreateDirectory(_directory);
                File.AppendAllText(CurrentFile, line.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch (Exception)
        {
            // O log nunca pode derrubar o app; sem disco ou sem permissão, a mensagem é perdida.
        }
    }

    private static void Prune()
    {
        try
        {
            lock (Gate)
            {
                if (!System.IO.Directory.Exists(_directory))
                    return;
                foreach (var old in System.IO.Directory.GetFiles(_directory, "lzn-*.log").OrderByDescending(path => path, StringComparer.Ordinal).Skip(KeepFiles))
                    File.Delete(old);
            }
        }
        catch (Exception) { }
    }
}
