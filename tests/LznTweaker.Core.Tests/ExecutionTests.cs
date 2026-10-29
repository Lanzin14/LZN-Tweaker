using System.Diagnostics;
using System.IO;
using LznTweaker.Core.Changes;
using LznTweaker.Core.Execution;
using LznTweaker.Core.Tweaks;
using Microsoft.Win32;

namespace LznTweaker.Core.Tests;

public sealed class ScriptRunnerServiceTests
{
    private const string ScriptHeader = "param($PayloadPath, $ResultPath, $ProgressPath)\n";

    private static Task<ScriptRunResult> Run(string body, TimeSpan? timeout = null, Action<string>? onProgress = null) =>
        BatchEngineHarness.CreateRunner().RunAsync(
            new ScriptRunRequest("unit", ScriptHeader + body, "{\"Value\":\"olá\"}", timeout ?? TimeSpan.FromMinutes(1), RequiresElevation: false),
            onProgress);

    [Fact]
    public async Task Payload_is_delivered_and_result_is_returned_in_utf8()
    {
        var result = await Run("$p = Get-Content -LiteralPath $PayloadPath -Raw -Encoding UTF8 | ConvertFrom-Json\n" +
                               "[IO.File]::WriteAllText($ResultPath, $p.Value + ' ção', [Text.UTF8Encoding]::new($false))");

        Assert.Equal(ScriptRunStatus.Completed, result.Status);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("olá ção", result.ResultJson);
    }

    [Fact]
    public async Task Missing_result_and_stderr_are_reported()
    {
        var result = await Run("[Console]::Error.WriteLine('falhou aqui'); exit 4");

        Assert.Equal(ScriptRunStatus.Completed, result.Status);
        Assert.Equal(4, result.ExitCode);
        Assert.Null(result.ResultJson);
        Assert.Contains("falhou aqui", result.ErrorOutput);
        Assert.Contains("código 4", result.Message);
    }

    [Fact]
    public async Task Timeout_kills_the_process()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await Run("Start-Sleep -Seconds 60", TimeSpan.FromSeconds(2));

        Assert.Equal(ScriptRunStatus.TimedOut, result.Status);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Progress_snapshots_are_published()
    {
        var snapshots = new List<string>();
        await Run("foreach ($i in 1..3) { [IO.File]::WriteAllText($ProgressPath, \"passo $i\"); Start-Sleep -Milliseconds 400 }\n" +
                  "[IO.File]::WriteAllText($ResultPath, 'ok')", onProgress: snapshots.Add);

        Assert.Contains("passo 3", snapshots);
        Assert.True(snapshots.Count >= 2, $"Instantâneos recebidos: {string.Join(", ", snapshots)}");
    }

    [Fact]
    public async Task Run_directory_is_deleted_after_execution()
    {
        var runner = BatchEngineHarness.CreateRunner();
        await runner.RunAsync(new ScriptRunRequest("cleanup", ScriptHeader + "[IO.File]::WriteAllText($ResultPath, $PSScriptRoot)", "{}", TimeSpan.FromMinutes(1), false));

        Assert.Empty(Directory.GetDirectories(runner.Workspace.RootPath, "cleanup-*"));
    }
}

public sealed class ElevatedWorkspaceTests
{
    [NonElevatedFact]
    public async Task Restricted_workspace_refuses_to_run_without_elevation()
    {
        var workspace = new ElevatedWorkspace(Path.Combine(Path.GetTempPath(), "LznTweakerTests", "Restricted", "Operations"), restrictToAdministrators: true);
        var result = await new ScriptRunnerService(workspace)
            .RunAsync(new ScriptRunRequest("x", "exit 0", "{}", TimeSpan.FromMinutes(1), RequiresElevation: false));

        Assert.Equal(ScriptRunStatus.StartFailed, result.Status);
        Assert.Contains("administrador", result.Message);
    }

    [Fact]
    public void User_writable_folder_is_not_considered_protected()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"LznTweakerTest_{Guid.NewGuid():N}"));
        try
        {
            Assert.False(ElevatedWorkspace.IsProtected(directory));
        }
        finally
        {
            directory.Delete();
        }
    }

    [Fact]
    public void System_folder_owned_by_trusted_installer_is_rejected_as_workspace()
    {
        // C:\Windows pertence ao TrustedInstaller: a verificação exige dono Administradores/SYSTEM.
        Assert.False(ElevatedWorkspace.IsProtected(new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.Windows))));
    }
}

public sealed class RestorePointGuardTests
{
    private const string Volume = @"\\?\Volume{357ce604-30b9-4c11-b4e4-6c9aac622333}\";

    [Fact]
    public void Policy_block_wins() =>
        Assert.Equal(RestorePointState.BlockedByPolicy, RestorePointGuard.Evaluate(true, [Volume + ":(C%3A)"], Volume).State);

    [Fact]
    public void Protected_system_volume_is_ready() =>
        Assert.Equal(RestorePointState.Ready, RestorePointGuard.Evaluate(false, [Volume + ":(C%3A)"], Volume).State);

    [Fact]
    public void Other_volumes_only_means_protection_disabled()
    {
        var readiness = RestorePointGuard.Evaluate(false, [@"\\?\Volume{00000000-0000-0000-0000-000000000000}\:(D%3A)"], Volume);
        Assert.Equal(RestorePointState.ProtectionDisabled, readiness.State);
        Assert.True(readiness.CanEnableProtection);
        Assert.False(readiness.CanAttempt);
    }

    [Fact]
    public void Unreadable_state_is_unknown_never_ready()
    {
        var readiness = RestorePointGuard.Evaluate(false, null, Volume);
        Assert.Equal(RestorePointState.Unknown, readiness.State);
        Assert.True(readiness.CanAttempt);
    }
}

/// <summary>Executor completo (catálogo real → payload → PowerShell → histórico), sem elevação.</summary>
public sealed class TweakBatchExecutorTests
{
    private const string ExplorerAdvanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    [NonElevatedFact]
    public async Task Batch_without_restore_point_is_blocked_and_recorded()
    {
        var historyPath = Path.Combine(Path.GetTempPath(), $"LznTweakerTest_{Guid.NewGuid():N}.json");
        object? originalHideFileExt;
        using (var key = Registry.CurrentUser.OpenSubKey(ExplorerAdvanced))
            originalHideFileExt = key?.GetValue("HideFileExt");
        try
        {
            var history = new TweakOperationHistory(historyPath);
            var executor = new TweakBatchExecutor(history, BatchEngineHarness.CreateRunner());

            // O ponto de restauração não foi selecionado: o executor o adiciona e, sem elevação, ele falha.
            var results = await executor.RunAsync([TweakCatalog.GetById(TweakCatalog.ShowFileExtensionsId)], []);

            Assert.Equal(TweakCatalog.CreateRestorePointId, results[0].TweakId);
            Assert.Equal("Falha ou não verificado", results[0].Outcome);
            Assert.Equal("Bloqueado", results[1].Outcome);
            Assert.Null(results[1].RollbackData);
            using (var key = Registry.CurrentUser.OpenSubKey(ExplorerAdvanced))
                Assert.Equal(originalHideFileExt, key?.GetValue("HideFileExt"));
            Assert.Equal(2, (await history.ReadAsync()).Count);
        }
        finally
        {
            // Rede de segurança caso o bloqueio falhe: devolve o valor original do usuário.
            using (var key = Registry.CurrentUser.OpenSubKey(ExplorerAdvanced, writable: true))
            {
                if (key is not null && originalHideFileExt is not null && !Equals(key.GetValue("HideFileExt"), originalHideFileExt))
                    key.SetValue("HideFileExt", originalHideFileExt, RegistryValueKind.DWord);
            }
            File.Delete(historyPath);
        }
    }
}

/// <summary>Sondas reais e somente leitura dos scripts de verificação do catálogo.</summary>
public sealed class CatalogScriptProbeTests
{
    private static string RunReadOnly(string script)
    {
        var result = BatchEngineHarness.CreateRunner().RunAsync(new ScriptRunRequest("probe",
            "param($PayloadPath, $ResultPath, $ProgressPath)\n$ErrorActionPreference = 'Stop'\n" +
            "$value = & { " + script + " }\n[IO.File]::WriteAllText($ResultPath, [string]($value -join '|'), [Text.UTF8Encoding]::new($false))",
            "{}", TimeSpan.FromMinutes(1), false)).GetAwaiter().GetResult();
        Assert.True(result.ResultJson is not null, $"{result.Message} {result.ErrorOutput}");
        return result.ResultJson!;
    }

    [Fact]
    public void Ultimate_plan_lookup_matches_powercfg_list_in_any_language()
    {
        var found = RunReadOnly(TweakCatalog.UltimatePlanLookup + "($ultimatePlans | ForEach-Object Guid)");
        var expected = RunReadOnly("@(powercfg /list | Where-Object { $_ -match 'Ultimate Performance|Desempenho M.{1,2}ximo' } | ForEach-Object { ([regex]::Match($_, '[0-9a-fA-F-]{36}')).Value })");
        Assert.Equal(expected, found);
    }

    [Theory]
    [InlineData(TweakCatalog.DisableHibernationId)]
    [InlineData(TweakCatalog.DisableTeredoId)]
    [InlineData(TweakCatalog.ActivateUltimatePerformanceId)]
    [InlineData(TweakCatalog.RemoveUltimatePerformanceId)]
    public void Verify_scripts_run_without_errors_and_return_a_boolean(string tweakId)
    {
        var verify = TweakCatalog.GetById(tweakId).VerifyScript!;
        Assert.Contains(RunReadOnly(verify).Split('|')[^1], new[] { "True", "False" });
    }
}
