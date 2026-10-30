using System.IO;
using System.Text.Json.Nodes;
using Microsoft.Win32;

namespace LznTweaker.Core.Tests;

/// <summary>Executa o script PowerShell real do motor contra uma chave isolada do HKCU.</summary>
public sealed class BatchEngineTests
{
    // GetValue em vez do cast (string?): o parser do graphify falha no cast e perde o arquivo inteiro.
    private static string? Text(JsonNode? node, string property) => node?[property]?.GetValue<string>();

    [NonElevatedFact]
    public void Tracked_applies_dword_existing_string_and_new_value_with_verification()
    {
        using var sandbox = new SandboxKey();
        var result = BatchEngineHarness.Run(new[] { new { TweakId = "t1", Name = "t1", Kind = "Tracked",
            RegistryChanges = new[] { sandbox.Change("DwordVal", RegistryValueKind.DWord, 0), sandbox.Change("StrVal", RegistryValueKind.String, "506"), sandbox.Change("NewVal", RegistryValueKind.DWord, 1) } } })[0];

        Assert.Equal("Sucesso verificado", Text(result, "Outcome"));
        Assert.Equal(0, sandbox.Read("DwordVal"));
        Assert.Equal("506", sandbox.Read("StrVal"));
        Assert.Equal(RegistryValueKind.String, sandbox.KindOf("StrVal"));
        Assert.Equal(1, sandbox.Read("NewVal"));
        var rollback = JsonNode.Parse(Text(result, "RollbackData")!)!;
        Assert.Equal(3, rollback["Registry"]!.AsArray().Count);
        Assert.NotNull(rollback["Services"]);
        Assert.NotNull(rollback["Tasks"]);
    }

    [NonElevatedFact]
    public void Undo_restores_previous_kind_and_value_and_removes_new_value()
    {
        using var sandbox = new SandboxKey();
        var applied = BatchEngineHarness.Run(new[] { new { TweakId = "t1", Name = "t1", Kind = "Tracked",
            RegistryChanges = new[] { sandbox.Change("DwordVal", RegistryValueKind.DWord, 0), sandbox.Change("StrVal", RegistryValueKind.String, "506"), sandbox.Change("NewVal", RegistryValueKind.DWord, 1) } } })[0];

        var undone = BatchEngineHarness.Run(new[] { new { TweakId = "t1", Name = "t1", Kind = "Undo", RollbackData = Text(applied, "RollbackData") } })[0];

        Assert.Equal("Reversão verificada", Text(undone, "Outcome"));
        Assert.Equal(1, sandbox.Read("DwordVal"));
        Assert.Equal("510", sandbox.Read("StrVal"));
        Assert.Equal(RegistryValueKind.String, sandbox.KindOf("StrVal"));
        Assert.Null(sandbox.KindOf("NewVal"));
    }

    [NonElevatedFact]
    public void Undo_refuses_when_a_value_changed_and_reverts_nothing()
    {
        using var sandbox = new SandboxKey();
        var applied = BatchEngineHarness.Run(new[] { new { TweakId = "t1", Name = "t1", Kind = "Tracked",
            RegistryChanges = new[] { sandbox.Change("DwordVal", RegistryValueKind.DWord, 0), sandbox.Change("StrVal", RegistryValueKind.String, "506") } } })[0];
        sandbox.Write("StrVal", "999", RegistryValueKind.String);

        var undone = BatchEngineHarness.Run(new[] { new { TweakId = "t1", Name = "t1", Kind = "Undo", RollbackData = Text(applied, "RollbackData") } })[0];

        Assert.NotEqual("Reversão verificada", Text(undone, "Outcome"));
        Assert.Equal("999", sandbox.Read("StrVal"));
        Assert.Equal(0, sandbox.Read("DwordVal"));
    }

    [NonElevatedFact]
    public void Failure_in_the_middle_reverts_parts_already_applied()
    {
        using var sandbox = new SandboxKey();
        var result = BatchEngineHarness.Run(new[] { new { TweakId = "t4", Name = "t4", Kind = "Tracked",
            RegistryChanges = new[] { sandbox.Change("DwordVal", RegistryValueKind.DWord, 0) },
            ServiceChanges = new[] { new { Name = "Spooler", TargetStartupType = "Boot" } } } })[0];

        Assert.Equal("Falha ou não verificado", Text(result, "Outcome"));
        Assert.Contains("não permitido", Text(result, "Message"));
        Assert.Contains("revertidas", Text(result, "RollbackOutcome"));
        Assert.Null(result!["RollbackData"]);
        Assert.Equal(1, sandbox.Read("DwordVal"));
    }

    [NonElevatedFact]
    public void Hklm_without_elevation_fails_honestly_and_reverts_hkcu()
    {
        using var sandbox = new SandboxKey();
        var result = BatchEngineHarness.Run(new[] { new { TweakId = "t5", Name = "t5", Kind = "Tracked",
            RegistryChanges = new[] { sandbox.Change("DwordVal", RegistryValueKind.DWord, 0) },
            HklmRegistryChanges = new[] { new { SubKey = @"SOFTWARE\LznTweakerTest", Name = "X", Kind = 4, Value = 1 } } } })[0];

        Assert.Equal("Falha ou não verificado", Text(result, "Outcome"));
        Assert.Equal(1, sandbox.Read("DwordVal"));
    }

    [NonElevatedFact]
    public void Unsupported_previous_kind_is_refused_without_change()
    {
        using var sandbox = new SandboxKey();
        var result = BatchEngineHarness.Run(new[] { new { TweakId = "t6", Name = "t6", Kind = "Tracked",
            RegistryChanges = new[] { sandbox.Change("BinVal", RegistryValueKind.DWord, 0) } } })[0];

        Assert.Equal("Falha ou não verificado", Text(result, "Outcome"));
        Assert.Equal(RegistryValueKind.Binary, sandbox.KindOf("BinVal"));
    }

    [NonElevatedFact]
    public void Missing_service_and_task_are_reported_not_tracked()
    {
        using var sandbox = new SandboxKey();
        var result = BatchEngineHarness.Run(new[] { new { TweakId = "t7", Name = "t7", Kind = "Tracked",
            RegistryChanges = new[] { sandbox.Change("DwordVal", RegistryValueKind.DWord, 0) },
            ServiceChanges = new[] { new { Name = "LznNoSuchService", TargetStartupType = "Disabled" } },
            ScheduledTasks = new[] { @"\LznTest\NoSuchTask" } } })[0];

        Assert.Equal("Sucesso verificado", Text(result, "Outcome"));
        Assert.Contains("LznNoSuchService ausente", Text(result, "Message"));
        Assert.Contains("NoSuchTask ausente", Text(result, "Message"));
        Assert.Empty(JsonNode.Parse(Text(result, "RollbackData")!)!["Services"]!.AsArray());
    }

    [NonElevatedFact]
    public void Legacy_array_history_is_still_reversible()
    {
        using var sandbox = new SandboxKey();
        sandbox.Write("DwordVal", 0, RegistryValueKind.DWord);
        var legacy = $$"""[{"SubKey":"{{sandbox.Path.Replace("\\", "\\\\")}}","Name":"DwordVal","OldExists":true,"OldValue":1,"OldKind":4,"AppliedValue":0,"AppliedKind":4}]""";

        var result = BatchEngineHarness.Run(new[] { new { TweakId = "t8", Name = "t8", Kind = "Undo", RollbackData = legacy } })[0];

        Assert.Equal("Reversão verificada", Text(result, "Outcome"));
        Assert.Equal(1, sandbox.Read("DwordVal"));
    }

    [Fact]
    public void Custom_scripts_check_exit_code_and_post_verification()
    {
        var results = BatchEngineHarness.Run(new object[]
        {
            new { TweakId = "s1", Name = "s1", Kind = "CustomScript", InvokeScript = "cmd.exe /c exit 3" },
            new { TweakId = "s2", Name = "s2", Kind = "CustomScript", InvokeScript = "cmd.exe /c exit 0", VerifyScript = "$false" },
            new { TweakId = "s3", Name = "s3", Kind = "CustomScript", InvokeScript = "cmd.exe /c exit 0" },
            new { TweakId = "s4", Name = "s4", Kind = "CustomScript", InvokeScript = "cmd.exe /c exit 0", VerifyScript = "Write-Output 'ruído'; $true" }
        });

        Assert.Equal("Falha ou não verificado", Text(results[0], "Outcome"));
        Assert.Contains("3", Text(results[0], "Message"));
        Assert.Equal("Falha ou não verificado", Text(results[1], "Outcome"));
        Assert.Equal("Executado sem verificação", Text(results[2], "Outcome"));
        Assert.Equal("Sucesso verificado", Text(results[3], "Outcome"));
    }

    [Fact]
    public void Temp_cleanup_only_removes_files_identical_to_the_preview()
    {
        var temp = Environment.GetEnvironmentVariable("TEMP")!;
        var directory = Path.Combine(temp, $"LznTweakerTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var changed = Path.Combine(directory, "alterado.txt");
            var identical = Path.Combine(directory, "igual.txt");
            File.WriteAllText(changed, "abc");
            File.WriteAllText(identical, "abc");
            var changedInfo = new FileInfo(changed);
            var identicalInfo = new FileInfo(identical);

            var result = BatchEngineHarness.Run(new[] { new { TweakId = "c1", Name = "c1", Kind = "DeleteTempFiles", TempRoot = Path.GetFullPath(temp),
                Files = new[] { new { FullPath = identical, identicalInfo.Length, LastWriteTimeUtcTicks = identicalInfo.LastWriteTimeUtc.Ticks },
                                new { FullPath = changed, Length = changedInfo.Length + 1, LastWriteTimeUtcTicks = changedInfo.LastWriteTimeUtc.Ticks },
                                new { FullPath = @"C:\Windows\win.ini", Length = 1L, LastWriteTimeUtcTicks = 0L } } } })[0];

            Assert.False(File.Exists(identical));
            Assert.True(File.Exists(changed));
            Assert.True(File.Exists(@"C:\Windows\win.ini"));
            Assert.Contains("Removidos: 1;", Text(result, "Message"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [NonElevatedFact]
    public void Failed_restore_point_blocks_every_other_operation()
    {
        // Sem elevação, Get-ComputerRestorePoint/Checkpoint-Computer falham: o lote precisa parar ali.
        using var sandbox = new SandboxKey();
        var results = BatchEngineHarness.Run(new object[]
        {
            new { TweakId = "rp", Name = "rp", Kind = "RestorePoint" },
            new { TweakId = "t9", Name = "t9", Kind = "Tracked", RegistryChanges = new[] { sandbox.Change("DwordVal", RegistryValueKind.DWord, 0) } },
            new { TweakId = "s9", Name = "s9", Kind = "CustomScript", InvokeScript = "cmd.exe /c exit 0" }
        }, requireRestorePoint: true);

        Assert.Equal("Falha ou não verificado", Text(results[0], "Outcome"));
        Assert.Equal("Bloqueado", Text(results[1], "Outcome"));
        Assert.Equal("Bloqueado", Text(results[2], "Outcome"));
        Assert.Contains("ponto de restauração obrigatório", Text(results[1], "Message"));
        Assert.Equal(1, sandbox.Read("DwordVal"));
    }

    [NonElevatedFact]
    public void Without_requirement_a_failed_restore_point_does_not_block()
    {
        using var sandbox = new SandboxKey();
        var results = BatchEngineHarness.Run(new object[]
        {
            new { TweakId = "rp", Name = "rp", Kind = "RestorePoint" },
            new { TweakId = "t10", Name = "t10", Kind = "Tracked", RegistryChanges = new[] { sandbox.Change("DwordVal", RegistryValueKind.DWord, 0) } }
        }, requireRestorePoint: false);

        Assert.Equal("Sucesso verificado", Text(results[1], "Outcome"));
    }
}
