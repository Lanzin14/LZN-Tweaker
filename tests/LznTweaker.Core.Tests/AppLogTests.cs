using System.IO;
using System.Runtime.CompilerServices;
using LznTweaker.Core.Diagnostics;

namespace LznTweaker.Core.Tests;

internal static class TestLogSetup
{
    /// <summary>Os testes nunca gravam no log real do usuário.</summary>
    [ModuleInitializer]
    internal static void RedirectLog() =>
        AppLog.Initialize(Path.Combine(Path.GetTempPath(), "LznTweakerTestLogs"));
}

public sealed class AppLogTests
{
    [Fact]
    public void Writes_lines_with_exception_and_keeps_only_recent_files()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"LznTweakerTest_{Guid.NewGuid():N}");
        var previous = AppLog.Directory;
        try
        {
            Directory.CreateDirectory(folder);
            for (var day = 1; day <= 20; day++)
                File.WriteAllText(Path.Combine(folder, $"lzn-202601{day:00}.log"), "antigo");
            AppLog.Initialize(folder);
            Assert.Equal(14, Directory.GetFiles(folder, "lzn-*.log").Length);
            Assert.False(File.Exists(Path.Combine(folder, "lzn-20260101.log")));

            AppLog.Error("Falha de teste\ncom duas linhas", new InvalidOperationException("motivo"));
            var text = File.ReadAllText(AppLog.CurrentFile);
            Assert.Contains("ERRO Falha de teste | com duas linhas", text);
            Assert.Contains("InvalidOperationException: motivo", text);
        }
        finally
        {
            AppLog.Initialize(previous);
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Never_throws_when_the_folder_cannot_be_written()
    {
        var previous = AppLog.Directory;
        try
        {
            // Caminho inválido (caractere proibido): a escrita falha, mas sem exceção.
            AppLog.Initialize(Path.Combine(Path.GetTempPath(), "inv\0alido"));
            AppLog.Error("não deve lançar");
        }
        finally
        {
            AppLog.Initialize(previous);
        }
    }
}
