using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.Json;
using LznTweaker.Core.Execution;
using LznTweaker.Core.Tweaks;
using Microsoft.Win32;

namespace LznTweaker.Core.Changes;

public enum RestorePointState
{
    /// <summary>A Proteção do Sistema está ativa no volume do Windows.</summary>
    Ready,
    /// <summary>A proteção está desativada no volume do Windows; pode ser ativada pelo app com consentimento.</summary>
    ProtectionDisabled,
    /// <summary>Uma política (DisableSR) proíbe a Restauração do Sistema; o app não altera políticas.</summary>
    BlockedByPolicy,
    Unsupported,
    /// <summary>Não foi possível confirmar; o lote ainda tentará criar o ponto e bloqueará se falhar.</summary>
    Unknown
}

public sealed record RestorePointReadiness(RestorePointState State, string Message)
{
    /// <summary>Ready ou Unknown: o lote pode tentar criar o ponto (e é bloqueado se a criação falhar).</summary>
    public bool CanAttempt => State is RestorePointState.Ready or RestorePointState.Unknown;
    public bool CanEnableProtection => State == RestorePointState.ProtectionDisabled;
}

/// <summary>
/// Garante a rede de segurança dos lotes: informa se o ponto de restauração obrigatório pode ser criado e,
/// com consentimento do usuário, ativa a Proteção do Sistema no volume do Windows.
/// A criação do ponto em si acontece dentro do lote (<see cref="TweakBatchExecutor"/>), que bloqueia o restante se ela falhar.
/// </summary>
public sealed class RestorePointGuard(ScriptRunnerService scriptRunner, TweakOperationHistory history)
{
    public const string EnableProtectionOperationId = "enable-system-protection";
    private const string PolicyKey = @"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore";
    // O Windows lista em SPP\Clients os volumes com a Proteção do Sistema ativa, no valor do cliente System Restore.
    private const string SppClientsKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SPP\Clients";
    private const string SystemRestoreClientValue = "{09F7EDC5-294E-4180-AF6A-FB0E6A0E9513}";

    public Task<RestorePointReadiness> CheckAsync(CancellationToken cancellationToken = default) =>
        Task.Run(Check, cancellationToken);

    /// <summary>Ativa a Proteção do Sistema no volume do Windows e confirma pela leitura posterior.</summary>
    public async Task<TweakOperationResult> EnableProtectionAsync(CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.Now;
        var before = Check();
        TweakOperationResult result;
        if (before.State == RestorePointState.Ready)
        {
            result = Record("Proteção já ativa", "Nada a alterar", before.Message);
        }
        else if (!before.CanEnableProtection)
        {
            result = Record(before.Message, "Não executado", "A proteção não pode ser ativada pelo app neste estado.");
        }
        else
        {
            var run = await scriptRunner.RunAsync(
                new ScriptRunRequest("protection", EmbeddedScripts.EnableSystemProtection, "{}", TimeSpan.FromMinutes(2), RequiresElevation: true),
                cancellationToken: cancellationToken);
            var scriptMessage = ReadMessage(run.ResultJson) ?? run.ErrorOutput ?? run.Message;
            var after = Check();
            result = after.State == RestorePointState.Ready
                ? Record("Proteção desativada", "Sucesso verificado",
                    "A Proteção do Sistema foi ativada no volume do Windows e confirmada pelo Windows. O espaço reservado segue o padrão do Windows e pode ser ajustado em Proteção do Sistema.")
                : Record("Proteção desativada", "Falha ou não verificado",
                    $"A ativação não foi confirmada ({scriptMessage}). Estado atual: {after.Message}");
        }

        await history.AppendAsync(result, cancellationToken);
        return result;

        TweakOperationResult Record(string previousState, string outcome, string message) => new(
            Guid.NewGuid(), EnableProtectionOperationId, startedAt, previousState, "Ativar Proteção do Sistema",
            outcome, message, RequiresRestart: false, RollbackMethod: null,
            RollbackOutcome: "Sem reversão automática: desativar a proteção apaga os pontos existentes; faça isso manualmente em Proteção do Sistema, se desejar.");
    }

    private static RestorePointReadiness Check()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240))
            return new RestorePointReadiness(RestorePointState.Unsupported, "Compatível somente com Windows 10/11.");

        bool policyBlocks;
        string[]? clients = null;
        var clientsReadable = true;
        try
        {
            using (var policy = Registry.LocalMachine.OpenSubKey(PolicyKey, writable: false))
                policyBlocks = Convert.ToInt32(policy?.GetValue("DisableSR", 0)) == 1;
            using var sppClients = Registry.LocalMachine.OpenSubKey(SppClientsKey, writable: false);
            clients = sppClients?.GetValue(SystemRestoreClientValue) as string[] ?? [];
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            policyBlocks = false;
            clientsReadable = false;
        }

        return Evaluate(policyBlocks, clientsReadable ? clients : null, GetSystemVolumeName());
    }

    /// <param name="clients">Entradas de SPP\Clients, ou null se não puderam ser lidas.</param>
    /// <param name="systemVolume">"\\?\Volume{GUID}\" do volume do Windows, ou null se desconhecido.</param>
    internal static RestorePointReadiness Evaluate(bool policyBlocks, string[]? clients, string? systemVolume)
    {
        if (policyBlocks)
            return new RestorePointReadiness(RestorePointState.BlockedByPolicy,
                "A política DisableSR bloqueia a Restauração do Sistema neste computador. O LznTweaker não altera políticas; nenhum lote pode ser executado.");
        if (clients is null || string.IsNullOrWhiteSpace(systemVolume))
            return new RestorePointReadiness(RestorePointState.Unknown,
                "Não foi possível confirmar o estado da Proteção do Sistema; o lote tentará criar o ponto e será bloqueado se não conseguir.");

        var volume = systemVolume.TrimEnd('\\');
        return clients.Any(entry => entry.Contains(volume, StringComparison.OrdinalIgnoreCase))
            ? new RestorePointReadiness(RestorePointState.Ready, "Proteção do Sistema ativa no volume do Windows; um ponto é criado antes de cada lote.")
            : new RestorePointReadiness(RestorePointState.ProtectionDisabled,
                "A Proteção do Sistema está desativada no volume do Windows; sem ela, nenhum lote pode ser executado.");
    }

    private static string? GetSystemVolumeName()
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory);
        if (string.IsNullOrEmpty(root))
            return null;
        var buffer = new StringBuilder(64);
        return GetVolumeNameForVolumeMountPoint(root, buffer, (uint)buffer.Capacity) ? buffer.ToString() : null;
    }

    private static string? ReadMessage(string? resultJson)
    {
        if (resultJson is null)
            return null;
        try
        {
            using var document = JsonDocument.Parse(resultJson);
            return document.RootElement.TryGetProperty("Message", out var message) ? message.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeNameForVolumeMountPoint(string volumeMountPoint, StringBuilder volumeName, uint bufferLength);
}
