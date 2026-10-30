using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using LznTweaker.Core.Diagnostics;

namespace LznTweaker.Core.Execution;

public enum ScriptRunStatus
{
    /// <summary>O processo terminou; verifique <see cref="ScriptRunResult.ResultJson"/> para saber se houve resultado.</summary>
    Completed,
    Canceled,
    TimedOut,
    ElevationDenied,
    StartFailed
}

/// <summary>
/// Um script PowerShell que recebe -PayloadPath, -ResultPath e -ProgressPath.
/// O script escreve o resultado final em ResultPath e, opcionalmente, instantâneos de progresso em ProgressPath.
/// </summary>
public sealed record ScriptRunRequest(string Name, string Script, string PayloadJson, TimeSpan Timeout, bool RequiresElevation);

public sealed record ScriptRunResult(ScriptRunStatus Status, int? ExitCode, string? ResultJson, string? ErrorOutput, string Message);

/// <summary>
/// Executa scripts PowerShell de forma coordenada: um processo por lote, arquivos em um
/// <see cref="ElevatedWorkspace"/>, timeout, cancelamento e acompanhamento de progresso.
/// Não interpreta o conteúdo do resultado: isso é responsabilidade de quem chama.
/// </summary>
/// <param name="allowElevationPrompt">
/// false nunca abre o UAC: operações que exigem administrador rodam sem elevação e falham de forma verificável (usado nos testes).
/// </param>
public sealed class ScriptRunnerService(ElevatedWorkspace workspace, bool allowElevationPrompt = true)
{
    private const string PowerShellRelativePath = @"WindowsPowerShell\v1.0\powershell.exe";
    private static readonly TimeSpan ProgressPollInterval = TimeSpan.FromMilliseconds(250);

    public ElevatedWorkspace Workspace { get; } = workspace;

    /// <param name="onProgress">Recebe o conteúdo do arquivo de progresso sempre que ele muda.</param>
    public async Task<ScriptRunResult> RunAsync(
        ScriptRunRequest request,
        Action<string>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var result = await RunCoreAsync(request, onProgress, cancellationToken);
        // Execuções que não terminaram normalmente, ou terminaram com erro do PowerShell, ficam no log para diagnóstico.
        if (result.Status != ScriptRunStatus.Completed || result.ErrorOutput is not null || result.ResultJson is null)
            AppLog.Warn($"Script '{request.Name}': {result.Status}, código {result.ExitCode?.ToString() ?? "-"}. {result.Message}" +
                        (result.ErrorOutput is null ? "" : $" Erro: {Truncate(result.ErrorOutput, 2000)}"));
        else
            AppLog.Info($"Script '{request.Name}': concluído.");
        return result;
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max] + "…";

    private async Task<ScriptRunResult> RunCoreAsync(
        ScriptRunRequest request,
        Action<string>? onProgress,
        CancellationToken cancellationToken)
    {
        ScriptRunDirectory run;
        try
        {
            run = Workspace.CreateRunDirectory(request.Name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new ScriptRunResult(ScriptRunStatus.StartFailed, null, null, null, $"O espaço de execução não pôde ser preparado: {ex.Message}");
        }

        using (run)
        {
            var scriptPath = run.GetFilePath("script.ps1");
            var payloadPath = run.GetFilePath("payload.json");
            var resultPath = run.GetFilePath("result.json");
            var progressPath = run.GetFilePath("progress.json");

            // O Windows PowerShell 5.1 só lê o script como UTF-8 se houver BOM.
            await File.WriteAllTextAsync(scriptPath, request.Script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);
            await File.WriteAllTextAsync(payloadPath, request.PayloadJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);

            // Com o app já elevado (manifesto), o processo filho herda a elevação e a saída de erro pode ser lida.
            var useRunAs = request.RequiresElevation && !ProcessElevation.IsElevated && allowElevationPrompt;
            using var process = new Process { StartInfo = CreateStartInfo(scriptPath, payloadPath, resultPath, progressPath, useRunAs) };

            try
            {
                if (!process.Start())
                    return new ScriptRunResult(ScriptRunStatus.StartFailed, null, null, null, "O Windows não iniciou o PowerShell.");
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return new ScriptRunResult(ScriptRunStatus.ElevationDenied, null, null, null, "A solicitação de administrador foi cancelada; nada foi executado.");
            }
            catch (Win32Exception ex)
            {
                return new ScriptRunResult(ScriptRunStatus.StartFailed, null, null, null, $"O PowerShell não pôde ser iniciado: {ex.Message}");
            }

            var errorTask = useRunAs ? Task.FromResult(string.Empty) : process.StandardError.ReadToEndAsync(CancellationToken.None);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(request.Timeout);
            string? lastProgress = null;
            try
            {
                var exitTask = process.WaitForExitAsync(timeout.Token);
                while (!exitTask.IsCompleted)
                {
                    lastProgress = PublishProgress(progressPath, lastProgress, onProgress);
                    await Task.WhenAny(exitTask, Task.Delay(ProgressPollInterval, timeout.Token));
                }
                await exitTask;
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                var canceled = cancellationToken.IsCancellationRequested;
                return new ScriptRunResult(
                    canceled ? ScriptRunStatus.Canceled : ScriptRunStatus.TimedOut, null, null, null,
                    canceled
                        ? "A execução foi cancelada; operações em andamento podem ter resultado incerto."
                        : $"A execução excedeu o limite de {request.Timeout.TotalMinutes:0} minuto(s); o resultado não foi confirmado.");
            }

            PublishProgress(progressPath, lastProgress, onProgress);
            var errorOutput = (await errorTask).Trim();
            var resultJson = File.Exists(resultPath) ? await File.ReadAllTextAsync(resultPath, CancellationToken.None) : null;
            var message = resultJson is not null
                ? "O script terminou e retornou um resultado."
                : process.ExitCode == 0
                    ? "O script terminou sem um arquivo de resultado verificável."
                    : $"O PowerShell terminou com código {process.ExitCode} sem resultado verificável.";
            return new ScriptRunResult(ScriptRunStatus.Completed, process.ExitCode, resultJson,
                errorOutput.Length == 0 ? null : errorOutput, message);
        }
    }

    private static ProcessStartInfo CreateStartInfo(string scriptPath, string payloadPath, string resultPath, string progressPath, bool useRunAs)
    {
        var info = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, PowerShellRelativePath),
            // Bypass vale só para este processo: sem ele, a política padrão do Windows cliente (Restricted) bloqueia -File.
            Arguments = $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\" " +
                        $"-PayloadPath \"{payloadPath}\" -ResultPath \"{resultPath}\" -ProgressPath \"{progressPath}\"",
            UseShellExecute = useRunAs,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        if (useRunAs)
        {
            info.Verb = "runas";
        }
        else
        {
            info.CreateNoWindow = true;
            info.RedirectStandardError = true;
            info.StandardErrorEncoding = Encoding.UTF8;
        }
        return info;
    }

    private static string? PublishProgress(string progressPath, string? lastProgress, Action<string>? onProgress)
    {
        if (onProgress is null || !File.Exists(progressPath))
            return lastProgress;
        try
        {
            var content = File.ReadAllText(progressPath);
            if (content.Length == 0 || string.Equals(content, lastProgress, StringComparison.Ordinal))
                return lastProgress;
            onProgress(content);
            return content;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // O script pode estar substituindo o arquivo; a próxima leitura tenta de novo.
            return lastProgress;
        }
    }
}
