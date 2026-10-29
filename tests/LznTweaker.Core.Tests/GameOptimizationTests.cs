using System.IO;
using System.Text.Json;
using LznTweaker.Core.Execution;
using LznTweaker.Core.GameOptimization;
using LznTweaker.Core.GameTuning;
using LznTweaker.Core.Tweaks;

namespace LznTweaker.Core.Tests;

/// <summary>Estrutura falsa de Steam + Riot em uma pasta temporária.</summary>
internal sealed class FakeGameEnvironment : IDisposable
{
    public FakeGameEnvironment()
    {
        Root = Path.Combine(Path.GetTempPath(), $"LznTweakerTest_{Guid.NewGuid():N}");
        SteamRoot = Path.Combine(Root, "Steam");
        Directory.CreateDirectory(Path.Combine(SteamRoot, "steamapps"));
        Directory.CreateDirectory(Path.Combine(SteamRoot, "config"));
        File.WriteAllText(Path.Combine(SteamRoot, "steamapps", "libraryfolders.vdf"),
            $"\"libraryfolders\" {{ \"0\" {{ \"path\" \"{SteamRoot.Replace("\\", "\\\\")}\" }} }}");
        File.WriteAllText(Path.Combine(SteamRoot, "config", "loginusers.vdf"),
            "\"users\" { \"76561198091286736\" { \"PersonaName\" \"Jogador\" \"MostRecent\" \"1\" } }");
        RiotMetadata = Path.Combine(Root, "Riot", "Metadata");
        History = new TweakOperationHistory(Path.Combine(Root, "history.json"));
    }

    public string Root { get; }
    public string SteamRoot { get; }
    public string RiotMetadata { get; }
    public TweakOperationHistory History { get; }
    public GameLocator Locator => new(new SteamLibraryLocator(() => SteamRoot), RiotMetadata);

    public string InstallSteamGame(string appId, string installDir, string exeRelative)
    {
        File.WriteAllText(Path.Combine(SteamRoot, "steamapps", $"appmanifest_{appId}.acf"), $"\"AppState\" {{ \"appid\" \"{appId}\" \"installdir\" \"{installDir}\" }}");
        var exe = Path.Combine(SteamRoot, "steamapps", "common", installDir, exeRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(exe)!);
        File.WriteAllText(exe, "exe");
        return exe;
    }

    public string CreateFolder(string relative, params string[] files)
    {
        var folder = Path.Combine(SteamRoot, relative);
        Directory.CreateDirectory(folder);
        foreach (var file in files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(folder, file))!);
            File.WriteAllText(Path.Combine(folder, file), "conteudo");
        }
        return folder;
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);
}

public sealed class GameLocatorTests
{
    [Fact]
    public void Steam_games_are_found_by_app_id_with_their_own_executable()
    {
        using var env = new FakeGameEnvironment();
        var exe = env.InstallSteamGame("730", "Counter-Strike Global Offensive", @"game\bin\win64\cs2.exe");

        var cs2 = env.Locator.Locate(SupportedGames.Cs2);
        var overwatch = env.Locator.Locate(SupportedGames.Overwatch2);

        Assert.Equal(exe, cs2.ExecutablePath);
        Assert.Equal(env.SteamRoot, cs2.SteamLibraryPath);
        Assert.False(overwatch.IsInstalled);
    }

    [Fact]
    public void Valorant_is_found_through_riot_metadata_never_through_steam()
    {
        using var env = new FakeGameEnvironment();
        var install = Path.Combine(env.Root, "Riot Games", "VALORANT", "live");
        Directory.CreateDirectory(Path.Combine(install, "ShooterGame", "Binaries", "Win64"));
        File.WriteAllText(Path.Combine(install, "ShooterGame", "Binaries", "Win64", "VALORANT-Win64-Shipping.exe"), "exe");
        Directory.CreateDirectory(Path.Combine(env.RiotMetadata, "valorant.live"));
        File.WriteAllText(Path.Combine(env.RiotMetadata, "valorant.live", "valorant.live.product_settings.yaml"),
            $"product_install_full_path: \"{install.Replace('\\', '/')}\"\nproduct_install_root: \"x\"\n");

        var valorant = env.Locator.Locate(SupportedGames.Valorant);

        Assert.True(valorant.IsInstalled);
        Assert.EndsWith("VALORANT-Win64-Shipping.exe", valorant.ExecutablePath);
        Assert.Null(valorant.SteamLibraryPath);
        Assert.Null(SupportedGames.Valorant.SteamAppId);
    }

    [Fact]
    public void Steam_accounts_map_steamid64_to_userdata_folder()
    {
        using var env = new FakeGameEnvironment();
        env.CreateFolder(@"userdata\131021008");
        env.CreateFolder(@"userdata\85747827");

        var accounts = SteamAccounts.List(env.SteamRoot);

        Assert.Equal("131021008", accounts[0].AccountId);
        Assert.Equal("Jogador", accounts[0].PersonaName);
        Assert.True(accounts[0].MostRecent);
        Assert.Null(accounts.Single(account => account.AccountId == "85747827").PersonaName);
    }
}

public sealed class ShaderCacheServiceTests
{
    [Fact]
    public async Task Scan_and_clean_only_known_locations_keeping_the_root_folder()
    {
        using var env = new FakeGameEnvironment();
        var local = Path.Combine(env.Root, "Local");
        var low = Path.Combine(env.Root, "LocalLow");
        Directory.CreateDirectory(Path.Combine(local, "D3DSCache", "sub"));
        File.WriteAllText(Path.Combine(local, "D3DSCache", "sub", "a.bin"), new string('x', 2048));
        Directory.CreateDirectory(Path.Combine(local, "AMD", "DxcCache"));
        File.WriteAllText(Path.Combine(local, "AMD", "DxcCache", "b.bin"), "y");
        var unrelated = Path.Combine(local, "AMD", "Outra");
        Directory.CreateDirectory(unrelated);
        File.WriteAllText(Path.Combine(unrelated, "keep.txt"), "z");
        var service = new ShaderCacheService(env.History, local, low);

        var locations = await service.ScanAsync();
        Assert.Equal(["amd-dxc", "d3d"], locations.Select(location => location.Id).Order());
        Assert.Equal(2048, locations.Single(location => location.Id == "d3d").Bytes);

        var forged = locations.Single(location => location.Id == "d3d") with { Path = unrelated };
        var result = await service.CleanAsync([forged, locations.Single(location => location.Id == "amd-dxc")]);

        Assert.Equal("Sucesso verificado", result.Outcome);
        Assert.True(File.Exists(Path.Combine(unrelated, "keep.txt")));
        Assert.True(File.Exists(Path.Combine(local, "D3DSCache", "sub", "a.bin"))); // o local forjado foi ignorado
        Assert.True(Directory.Exists(Path.Combine(local, "AMD", "DxcCache")));
        Assert.Empty(Directory.GetFiles(Path.Combine(local, "AMD", "DxcCache")));
        Assert.Contains("recompilar", result.Message);
    }

    [Fact]
    public async Task Files_in_use_are_kept_and_counted()
    {
        using var env = new FakeGameEnvironment();
        var local = Path.Combine(env.Root, "Local");
        Directory.CreateDirectory(Path.Combine(local, "D3DSCache"));
        var locked = Path.Combine(local, "D3DSCache", "locked.bin");
        File.WriteAllText(locked, "x");
        var service = new ShaderCacheService(env.History, local, Path.Combine(env.Root, "LocalLow"));
        var locations = await service.ScanAsync();

        TweakOperationResult result;
        using (File.Open(locked, FileMode.Open, FileAccess.Read, FileShare.None))
            result = await service.CleanAsync(locations);

        Assert.True(File.Exists(locked));
        Assert.Contains("1 em uso", result.Message);
    }
}

public sealed class SteamFolderBackupServiceTests
{
    private static (FakeGameEnvironment Env, DetectedGame Cs2) SetupCs2()
    {
        var env = new FakeGameEnvironment();
        env.InstallSteamGame("730", "Counter-Strike Global Offensive", @"game\bin\win64\cs2.exe");
        env.CreateFolder(@"steamapps\shadercache\730", "cache.bin");
        env.CreateFolder(@"userdata\131021008\730", @"local\cfg\cs2_user_keys_0_slot0.vcfg");
        return (env, env.Locator.Locate(SupportedGames.Cs2));
    }

    [Fact]
    public void Cs2_targets_include_shader_cache_and_each_account_config_other_games_never_get_userdata()
    {
        var (env, cs2) = SetupCs2();
        using var _ = env;
        env.InstallSteamGame("2357570", "Overwatch", "Overwatch.exe");
        env.CreateFolder(@"steamapps\shadercache\2357570", "x.bin");
        env.CreateFolder(@"userdata\131021008\2357570", "ow.cfg");
        var service = new SteamFolderBackupService(env.History, _ => false);

        var cs2Targets = service.GetTargets(cs2, env.SteamRoot);
        var owTargets = service.GetTargets(env.Locator.Locate(SupportedGames.Overwatch2), env.SteamRoot);

        Assert.Equal([SteamFolderKind.ShaderCache, SteamFolderKind.UserConfig], cs2Targets.Select(target => target.Kind));
        Assert.Contains("Jogador", cs2Targets[1].Label);
        Assert.Equal(SteamFolderKind.ShaderCache, Assert.Single(owTargets).Kind);
    }

    [Fact]
    public async Task Reset_renames_to_backup_and_restore_brings_it_back_keeping_the_recreated_folder()
    {
        var (env, cs2) = SetupCs2();
        using var _ = env;
        var service = new SteamFolderBackupService(env.History, _ => false);
        var userData = service.GetTargets(cs2, env.SteamRoot).Single(target => target.Kind == SteamFolderKind.UserConfig);

        var reset = await service.BackupAndResetAsync(userData);
        Assert.Equal("Sucesso verificado", reset.Outcome);
        Assert.False(Directory.Exists(userData.FolderPath));
        var backup = Assert.Single(await service.GetBackupsAsync());
        Assert.True(File.Exists(Path.Combine(backup.BackupPath, "local", "cfg", "cs2_user_keys_0_slot0.vcfg")));

        Directory.CreateDirectory(Path.Combine(userData.FolderPath, "local", "cfg")); // o CS2 recriou as configurações padrão
        var restore = await service.RestoreAsync(backup);

        Assert.Equal("Reversão verificada", restore.Outcome);
        Assert.True(File.Exists(Path.Combine(userData.FolderPath, "local", "cfg", "cs2_user_keys_0_slot0.vcfg")));
        Assert.Single(Directory.GetDirectories(Path.GetDirectoryName(userData.FolderPath)!, "730.lzntweaker-recriada-*"));
        Assert.Empty(await service.GetBackupsAsync());
    }

    [Fact]
    public async Task Running_steam_blocks_without_touching_anything()
    {
        var (env, cs2) = SetupCs2();
        using var _ = env;
        var service = new SteamFolderBackupService(env.History, name => name == "steam");
        var target = service.GetTargets(cs2, env.SteamRoot)[0];

        var result = await service.BackupAndResetAsync(target);

        Assert.Equal("Bloqueado", result.Outcome);
        Assert.True(Directory.Exists(target.FolderPath));
    }
}

public sealed class DiagnosticRulesTests
{
    private static DiagnosticFacts Facts(int? overlayTestMode = null, int? gameMode = null, Dictionary<string, string>? bcd = null, bool bcdReadable = true,
        long free = 500, int modules = 2, int configured = 3200, int rated = 2400, string? layers = null, string scheme = "(8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c)")
        => new(gameMode, 0, 0, 2, 0, 0, overlayTestMode, null, scheme, bcdReadable, bcd ?? [], 0,
            [new VolumeFact("C", 1000, free, "SSD", "NVMe")],
            Enumerable.Range(0, modules).Select(index => new MemoryFact($"DIMM {index}", $"CHANNEL {(char)('A' + index)}", 8L << 30, rated, configured, null, null)).ToList(),
            2, "5.17", "2025-09-12", "Placa", ["AMD Radeon RX 6600"], "AMD Ryzen 5 5600X", false,
            [new ExecutableFact(@"C:\cs2.exe", null, layers, null)]);

    private static readonly DetectedGame Cs2 = new(SupportedGames.Cs2, @"C:\Games\CS2", @"C:\cs2.exe", @"C:\Steam");
    private static readonly DetectedGame Valorant = new(SupportedGames.Valorant, null, null, null);

    private static DiagnosticItem Get(IReadOnlyList<DiagnosticItem> items, string id) => items.Single(item => item.Id == id);

    [Fact]
    public void Real_pc_case_mpo_disabled_game_mode_off_and_low_disk_space_need_attention()
    {
        var items = DiagnosticRules.Evaluate(Facts(overlayTestMode: 5, gameMode: 0, free: 80), [Cs2, Valorant], 12);

        Assert.Equal(DiagnosticStatus.Attention, Get(items, "windows-mpo").Status);
        Assert.Equal(DiagnosticStatus.Attention, Get(items, "windows-game-mode").Status);
        Assert.Equal(DiagnosticStatus.Attention, Get(items, "disk-C-free").Status);
        Assert.Contains("Counter-Strike 2", Get(items, "disk-C-free").GamesAffected);
        Assert.Equal(DiagnosticStatus.Attention, Get(items, "windows-startup").Status);
    }

    [Fact]
    public void Defaults_are_ok_and_hags_is_conditional()
    {
        var items = DiagnosticRules.Evaluate(Facts(), [Cs2], null);

        Assert.Equal(DiagnosticStatus.Ok, Get(items, "windows-mpo").Status);
        Assert.Equal(DiagnosticStatus.Ok, Get(items, "windows-game-mode").Status);
        Assert.Equal(DiagnosticStatus.Ok, Get(items, "windows-bcd-timer").Status);
        Assert.Equal(DiagnosticStatus.Info, Get(items, "windows-hags").Status);
        Assert.Contains("geração de quadros", Get(items, "windows-hags").Recommendation);
        Assert.Equal(DiagnosticStatus.Ok, Get(items, "hw-ram-xmp").Status);
        Assert.Equal(DiagnosticStatus.Ok, Get(items, "hw-ram-channels").Status);
    }

    [Fact]
    public void Bcd_overrides_show_the_exact_manual_commands()
    {
        var items = DiagnosticRules.Evaluate(Facts(bcd: new() { ["useplatformclock"] = "Yes", ["disabledynamictick"] = "Yes" }), [Cs2], null);
        var bcd = Get(items, "windows-bcd-timer");

        Assert.Equal(DiagnosticStatus.Attention, bcd.Status);
        Assert.Contains("bcdedit /deletevalue useplatformclock", bcd.Recommendation);
        Assert.Contains("bcdedit /deletevalue disabledynamictick", bcd.Recommendation);
        Assert.Equal(DiagnosticStatus.Unknown, Get(DiagnosticRules.Evaluate(Facts(bcdReadable: false), [Cs2], null), "windows-bcd-timer").Status);
    }

    [Fact]
    public void Single_module_jedec_memory_and_disabled_fullscreen_optimizations_are_flagged()
    {
        var items = DiagnosticRules.Evaluate(Facts(modules: 1, configured: 2400, layers: "~ DISABLEDXMAXIMIZEDWINDOWEDMODE"), [Cs2], null);

        Assert.Equal(DiagnosticStatus.Attention, Get(items, "hw-ram-channels").Status);
        Assert.Equal(DiagnosticStatus.Info, Get(items, "hw-ram-xmp").Status);
        Assert.Equal(DiagnosticStatus.Attention, Get(items, "cs2-fullscreen-optimizations").Status);
        Assert.Equal(OptimizationArea.Cs2, Get(items, "cs2-gpu-preference").Area);
    }

    [Theory]
    [InlineData("AppStatus=0;", "Padrão do Windows (não definido)", DiagnosticStatus.Info)]
    [InlineData("SwapEffectUpgradeEnable=1;GpuPreference=2;", "Alto desempenho", DiagnosticStatus.Ok)]
    [InlineData("GpuPreference=1;", "Economia de energia", DiagnosticStatus.Info)]
    public void Gpu_preference_is_read_from_the_setting_list(string raw, string expected, DiagnosticStatus status)
    {
        var facts = Facts() with { Executables = [new ExecutableFact(@"C:\cs2.exe", raw, null, null)] };
        var item = Get(DiagnosticRules.Evaluate(facts, [Cs2], null), "cs2-gpu-preference");

        Assert.Equal(expected, item.CurrentState);
        Assert.Equal(status, item.Status);
    }

    [Fact]
    public void Games_that_are_not_installed_get_no_executable_items() =>
        Assert.DoesNotContain(DiagnosticRules.Evaluate(Facts(), [Valorant], null), item => item.Area == OptimizationArea.Valorant);

    [Fact]
    public void Balanced_plan_needs_attention() =>
        Assert.Equal(DiagnosticStatus.Attention,
            Get(DiagnosticRules.Evaluate(Facts(scheme: "GUID: 381b4222-f694-41f0-9685-ff5bb260df2e (Equilibrado)"), [Cs2], null), "windows-power-plan").Status);

    [NonElevatedFact]
    public void Real_diagnostics_script_runs_read_only_and_is_understood()
    {
        var payload = JsonSerializer.Serialize(new { DriveLetters = new[] { "C" }, Executables = Array.Empty<string>() });
        var run = BatchEngineHarness.CreateRunner()
            .RunAsync(new ScriptRunRequest("diag", EmbeddedScripts.GamingDiagnostics, payload, TimeSpan.FromMinutes(2), RequiresElevation: false))
            .GetAwaiter().GetResult();
        Assert.True(run.ResultJson is not null, $"{run.Message} {run.ErrorOutput}");

        var facts = JsonSerializer.Deserialize<DiagnosticFacts>(run.ResultJson!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var items = DiagnosticRules.Evaluate(facts, [], null);

        Assert.NotEmpty(facts.Memory!);
        Assert.Equal(DiagnosticStatus.Unknown, Get(items, "windows-bcd-timer").Status); // sem elevação o BCD não é lido
        Assert.Contains(items, item => item.Id == "disk-C-free");
    }
}

public sealed class GuidanceCatalogTests
{
    [Fact]
    public void Every_area_has_guidance() =>
        Assert.All(Enum.GetValues<OptimizationArea>(), area => Assert.Contains(GuidanceCatalog.All, item => item.Area == area));

    [Fact]
    public void Valorant_guidance_never_applies_cs2_paths_or_touches_vanguard()
    {
        var valorant = GuidanceCatalog.All.Where(item => item.Area == OptimizationArea.Valorant && !item.Refused).ToArray();

        Assert.All(valorant, item => Assert.DoesNotContain("730", string.Join(" ", item.Steps)));
        Assert.All(valorant, item => Assert.DoesNotContain("userdata", string.Join(" ", item.Steps), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(valorant, item => item.Summary.Contains("nunca desativa", StringComparison.Ordinal));
    }

    [Fact]
    public void Items_without_evidence_are_marked_as_such()
    {
        Assert.Equal("Sem evidência", GuidanceCatalog.All.Single(item => item.Id == "g-cs2-network").Evidence);
        Assert.True(GuidanceCatalog.All.Single(item => item.Id == "g-cs2-profile-inspector").Refused);
    }

    [Fact]
    public void Ids_are_unique() =>
        Assert.Equal(GuidanceCatalog.All.Count, GuidanceCatalog.All.Select(item => item.Id).Distinct().Count());
}
