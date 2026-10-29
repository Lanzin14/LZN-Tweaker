using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using LznTweaker.Core.Tweaks;

namespace LznTweaker.Core.Maintenance;

public enum SystemTool
{
    FlushDns,
    ConnectivityTest,
    ShowNetworkConfiguration,
    ResetWinsock,
    SystemFileCheck,
    DismScanHealth,
    DismRestoreHealth,
    CheckDiskScan,
    OpenTroubleshooters,
    OpenSystemRestore,
    OpenPowerOptions
}

/// <summary>
/// Ferramentas nativas de diagnóstico e reparo. A lista é fixa: o app nunca executa um comando digitado pelo usuário.
/// Reparos longos (SFC, DISM, CHKDSK) abrem um console visível para o usuário acompanhar a saída real.
/// </summary>
public sealed class SystemToolsService(TweakOperationHistory history)
{
    public const string OperationPrefix = "tool:";

    public async Task<TweakOperationResult> RunAsync(SystemTool tool, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.Now;
        var (outcome, message) = tool switch
        {
            SystemTool.FlushDns => await RunCapturedAsync("ipconfig.exe", "/flushdns", "Cache DNS limpo pelo ipconfig.", cancellationToken),
            SystemTool.ResetWinsock => await RunCapturedAsync("netsh.exe", "winsock reset",
                "Catálogo Winsock redefinido. Reinicie o PC para concluir.", cancellationToken),
            SystemTool.ConnectivityTest => await TestConnectivityAsync(cancellationToken),
            SystemTool.ShowNetworkConfiguration => OpenConsole("ipconfig /all"),
            SystemTool.SystemFileCheck => OpenConsole("sfc /scannow"),
            SystemTool.DismScanHealth => OpenConsole("DISM /Online /Cleanup-Image /ScanHealth"),
            SystemTool.DismRestoreHealth => OpenConsole("DISM /Online /Cleanup-Image /RestoreHealth"),
            SystemTool.CheckDiskScan => OpenConsole($"chkdsk {Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\')} /scan"),
            SystemTool.OpenTroubleshooters => OpenShell("ms-settings:troubleshoot"),
            SystemTool.OpenSystemRestore => OpenShell(Path.Combine(Environment.SystemDirectory, "rstrui.exe")),
            SystemTool.OpenPowerOptions => OpenShell(Path.Combine(Environment.SystemDirectory, "powercfg.cpl")),
            _ => ("Falha ou não verificado", "Ferramenta desconhecida.")
        };

        var result = new TweakOperationResult(Guid.NewGuid(), OperationPrefix + tool, startedAt, "Não se aplica", Describe(tool), outcome, message,
            RequiresRestart: tool == SystemTool.ResetWinsock, RollbackMethod: null, RollbackOutcome: "Não se aplica");
        await history.AppendAsync(result, cancellationToken);
        return result;
    }

    public static string Describe(SystemTool tool) => tool switch
    {
        SystemTool.FlushDns => "Limpar cache DNS",
        SystemTool.ConnectivityTest => "Teste de conectividade",
        SystemTool.ShowNetworkConfiguration => "Ver configuração de rede",
        SystemTool.ResetWinsock => "Redefinir Winsock",
        SystemTool.SystemFileCheck => "Verificar arquivos do sistema (SFC)",
        SystemTool.DismScanHealth => "Verificar imagem do Windows (DISM ScanHealth)",
        SystemTool.DismRestoreHealth => "Reparar imagem do Windows (DISM RestoreHealth)",
        SystemTool.CheckDiskScan => "Verificar disco do sistema (CHKDSK /scan)",
        SystemTool.OpenTroubleshooters => "Abrir solução de problemas do Windows",
        SystemTool.OpenSystemRestore => "Abrir Restauração do Sistema",
        SystemTool.OpenPowerOptions => "Abrir Opções de Energia",
        _ => tool.ToString()
    };

    private static async Task<(string, string)> RunCapturedAsync(string fileName, string arguments, string successMessage, CancellationToken cancellationToken)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, fileName), arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            })!;
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0
                ? ("Sucesso verificado", successMessage)
                : ("Falha ou não verificado", $"O comando terminou com código {process.ExitCode}. {output.Trim()}");
        }
        catch (Win32Exception ex)
        {
            return ("Falha ou não verificado", $"Não foi possível executar {fileName}: {ex.Message}");
        }
    }

    private static async Task<(string, string)> TestConnectivityAsync(CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        var ok = 0;
        using var ping = new Ping();
        foreach (var (label, host) in new[] { ("Cloudflare", "1.1.1.1"), ("Google", "8.8.8.8") })
        {
            try
            {
                var reply = await ping.SendPingAsync(host, TimeSpan.FromSeconds(2), cancellationToken: cancellationToken);
                if (reply.Status == IPStatus.Success)
                {
                    ok++;
                    lines.Add($"{label} ({host}): {reply.RoundtripTime} ms");
                }
                else
                {
                    lines.Add($"{label} ({host}): {reply.Status}");
                }
            }
            catch (PingException ex)
            {
                lines.Add($"{label} ({host}): {ex.Message}");
            }
        }
        try
        {
            var addresses = await Dns.GetHostAddressesAsync("www.microsoft.com", cancellationToken);
            lines.Add($"DNS: www.microsoft.com resolvido ({addresses.Length} endereço(s))");
            ok++;
        }
        catch (Exception ex) when (ex is System.Net.Sockets.SocketException or ArgumentException)
        {
            lines.Add($"DNS: falhou ({ex.Message})");
        }
        return (ok == 3 ? "Sucesso verificado" : "Falha ou não verificado", string.Join(" · ", lines));
    }

    private static (string, string) OpenConsole(string command)
    {
        try
        {
            // Console visível: o usuário vê a saída real. O app já está elevado, então o console também está.
            Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), $"/k {command}") { UseShellExecute = true });
            return ("Executado sem verificação", $"\"{command}\" foi aberto em um console; acompanhe o resultado e feche a janela ao terminar.");
        }
        catch (Win32Exception ex)
        {
            return ("Falha ou não verificado", $"Não foi possível abrir o console: {ex.Message}");
        }
    }

    private static (string, string) OpenShell(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return ("Executado sem verificação", "Ferramenta do Windows aberta.");
        }
        catch (Win32Exception ex)
        {
            return ("Falha ou não verificado", $"Não foi possível abrir: {ex.Message}");
        }
    }
}
