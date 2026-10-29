using Microsoft.Win32;
using System.Security;
using System.IO;

namespace LznTweaker.Core.Tweaks;

public sealed record TweakCapability(TweakSystemState State, string Message, bool RequiresAdministrator);

/// <summary>
/// Detecta, somente para leitura, se a criação do ponto de restauração está bloqueada.
/// A criação em si é feita pelo <see cref="TweakBatchExecutor"/>, dentro do lote coordenado.
/// </summary>
public sealed class SystemRestorePointHandler
{
    private const string PolicyKey = @"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore";

    public Task<TweakCapability> DetectAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Detect(), cancellationToken);

    private static TweakCapability Detect()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240))
            return new TweakCapability(TweakSystemState.Unsupported, "Compatível somente com Windows cliente suportado (Windows 10/11).", true);

        try
        {
            using var policy = Registry.LocalMachine.OpenSubKey(PolicyKey, writable: false);
            if (Convert.ToInt32(policy?.GetValue("DisableSR", 0)) == 1)
            {
                return new TweakCapability(TweakSystemState.BlockedByPolicy,
                    "A política DisableSR bloqueia a Restauração do Sistema. O LznTweaker não altera políticas.", true);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
        {
            return new TweakCapability(TweakSystemState.Unknown,
                "Não foi possível consultar a política de proteção. O resultado só poderá ser conhecido na tentativa elevada.", true);
        }

        // A existência de pontos ou de configurações WMI não prova que a proteção está habilitada
        // para o volume do Windows. Não inferimos Enabled/Disabled a partir de dados incompletos.
        return new TweakCapability(TweakSystemState.Unknown,
            "Estado da Proteção do Sistema não confirmado. Ao aplicar, o Windows verificará a disponibilidade; a proteção não será habilitada automaticamente.", true);
    }
}
