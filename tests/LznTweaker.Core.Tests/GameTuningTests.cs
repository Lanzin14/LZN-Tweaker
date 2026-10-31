using System.IO;
using System.Text;
using LznTweaker.Core.GameTuning;
using LznTweaker.Core.Tweaks;

namespace LznTweaker.Core.Tests;

public sealed class KeyValuesReaderTests
{
    [Fact]
    public void Parses_quoted_unquoted_comments_conditionals_and_nested_blocks()
    {
        var nodes = KeyValuesReader.Parse("""
            // comentário
            "GameInfo"
            {
                game        "citadel"   // inline
                FileSystem
                {
                    SearchPaths
                    {
                        Game citadel/addons
                        Game  "citadel"  [$WIN64]
                    }
                }
            }
            """);

        var gameInfo = Assert.Single(nodes);
        Assert.Equal("citadel", gameInfo.ChildValue("game"));
        var searchPaths = gameInfo.Child("FileSystem")!.Child("SearchPaths")!.Children;
        Assert.Equal(["citadel/addons", "citadel"], searchPaths.Select(node => node.Value));
    }

    [Fact]
    public void Parses_steam_library_folders()
    {
        var root = KeyValuesReader.Parse("""
            "libraryfolders"
            {
                "0" { "path" "C:\\Program Files (x86)\\Steam" "apps" { "1422450" "1" } }
                "1" { "path" "D:\\SteamLibrary" }
            }
            """).Single();

        Assert.Equal([@"C:\Program Files (x86)\Steam", @"D:\SteamLibrary"], root.Children.Select(node => node.ChildValue("path")));
    }
}

public sealed class GameTuningServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"LznTweakerTest_{Guid.NewGuid():N}");
    private readonly string _installRoot;
    private readonly string _gameInfoPath;
    private readonly string _stateDirectory;
    private string _profileContent = string.Empty;
    private readonly TweakOperationHistory _history;
    private bool _gameRunning;

    public GameTuningServiceTests()
    {
        _installRoot = Path.Combine(_root, "steamapps", "common", "Deadlock");
        Directory.CreateDirectory(Path.Combine(_installRoot, "game", "citadel"));
        _gameInfoPath = Path.Combine(_installRoot, "game", "citadel", "gameinfo.gi");
        File.WriteAllText(_gameInfoPath, GameInfo("original"));
        _stateDirectory = Path.Combine(_root, "state");
        _history = new TweakOperationHistory(Path.Combine(_root, "history.json"));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private GameTuningService CreateService() =>
        new(_history, new SteamLibraryLocator(() => null), _stateDirectory, () => _gameRunning, _ => Encoding.UTF8.GetBytes(_profileContent));

    private DeadlockInstallation Installation => CreateService().FromUserSelection(_installRoot, out _)!;

    internal static string GameInfo(string marker, string extraSearchPath = "") =>
        $$"""
        "GameInfo"
        {
            game "citadel"
            FileSystem
            {
                SearchPaths
                {
                    Game citadel/addons
                    Mod citadel
                    Game citadel
                    Game core
                    {{extraSearchPath}}
                }
            }
            ConVars
            {
                "marker" "{{marker}}"
            }
        }
        """ + "\n" + string.Concat(Enumerable.Repeat($"// preenchimento {marker}\n", 60));

    private async Task<TweakOperationResult> ApplyAsync(GameTuningService service, string profileId, string content)
    {
        _profileContent = content;
        var prepared = service.Prepare(DeadlockProfiles.GetById(profileId), Installation);
        return await service.ApplyAsync(Installation, prepared);
    }

    [Fact]
    public async Task Apply_replaces_file_keeps_original_and_records_history()
    {
        var service = CreateService();
        var result = await ApplyAsync(service, DeadlockProfiles.RecommendedId, GameInfo("recomendada"));

        Assert.Equal("Sucesso verificado", result.Outcome);
        Assert.Contains("\"recomendada\"", File.ReadAllText(_gameInfoPath));
        Assert.Contains("\"original\"", File.ReadAllText(Path.Combine(_stateDirectory, "gameinfo.gi.original")));
        Assert.Equal(GameConfigState.Applied, service.GetStatus(Installation).State);
        Assert.Contains(await _history.ReadAsync(), entry => entry.TweakId == GameTuningService.DeadlockOperationId && entry.Outcome == "Sucesso verificado");
        Assert.Contains("27/10/2026", result.Message.Replace("10/27/2026", "27/10/2026"));
    }

    [Fact]
    public async Task Switching_profiles_keeps_the_first_original_and_undo_restores_it()
    {
        var service = CreateService();
        await ApplyAsync(service, DeadlockProfiles.RecommendedId, GameInfo("recomendada"));
        await ApplyAsync(service, DeadlockProfiles.LowEndId, GameInfo("lowend"));

        var undo = await service.UndoAsync(Installation);

        Assert.Equal("Reversão verificada", undo.Outcome);
        Assert.Contains("\"original\"", File.ReadAllText(_gameInfoPath));
        Assert.False(File.Exists(Path.Combine(_stateDirectory, "gameinfo.gi.original")));
        Assert.Equal(GameConfigState.NotApplied, service.GetStatus(Installation).State);
    }

    [Fact]
    public async Task Undo_after_game_update_refuses_and_never_overwrites_the_new_file()
    {
        var service = CreateService();
        await ApplyAsync(service, DeadlockProfiles.RecommendedId, GameInfo("recomendada"));
        File.WriteAllText(_gameInfoPath, GameInfo("patch-novo")); // a Steam reinstalou o arquivo

        Assert.Equal(GameConfigState.ChangedOutsideApp, service.GetStatus(Installation).State);
        var undo = await service.UndoAsync(Installation);

        Assert.Equal("Reversão recusada", undo.Outcome);
        Assert.Contains("\"patch-novo\"", File.ReadAllText(_gameInfoPath));
        Assert.Single(Directory.GetFiles(_stateDirectory, "gameinfo.gi.original-*"));
        Assert.Equal(GameConfigState.NotApplied, service.GetStatus(Installation).State);
    }

    [Fact]
    public async Task Applying_after_game_update_renews_the_backup_with_the_new_file()
    {
        var service = CreateService();
        await ApplyAsync(service, DeadlockProfiles.RecommendedId, GameInfo("recomendada"));
        File.WriteAllText(_gameInfoPath, GameInfo("patch-novo"));

        await ApplyAsync(service, DeadlockProfiles.RecommendedId, GameInfo("recomendada-v2"));
        await service.UndoAsync(Installation);

        Assert.Contains("\"patch-novo\"", File.ReadAllText(_gameInfoPath));
    }

    [Fact]
    public async Task Running_game_blocks_apply_and_undo()
    {
        var service = CreateService();
        _gameRunning = true;

        var result = await ApplyAsync(service, DeadlockProfiles.RecommendedId, GameInfo("recomendada"));

        Assert.Equal("Bloqueado", result.Outcome);
        Assert.Contains("\"original\"", File.ReadAllText(_gameInfoPath));
    }

    [Fact]
    public void Html_or_truncated_profile_is_rejected_before_anything_changes()
    {
        var service = CreateService();
        _profileContent = "<!DOCTYPE html><html>" + new string(' ', 2000) + "</html>";
        Assert.Throws<InvalidDataException>(() => service.Prepare(DeadlockProfiles.GetById(DeadlockProfiles.RecommendedId), Installation));

        _profileContent = "\"GameInfo\" { game \"citadel\" }" + new string(' ', 2000);
        Assert.Throws<InvalidDataException>(() => service.Prepare(DeadlockProfiles.GetById(DeadlockProfiles.RecommendedId), Installation));

        Assert.Contains("\"original\"", File.ReadAllText(_gameInfoPath));
        Assert.False(Directory.Exists(_stateDirectory));
    }

    [Fact]
    public void Missing_search_path_in_profile_is_reported_as_compatibility_warning()
    {
        File.WriteAllText(_gameInfoPath, GameInfo("original", "Game citadel/conteudo_novo"));
        _profileContent = GameInfo("recomendada");

        var download = CreateService().Prepare(DeadlockProfiles.GetById(DeadlockProfiles.RecommendedId), Installation);

        var warning = Assert.Single(download.CompatibilityWarnings);
        Assert.Contains("citadel/conteudo_novo", warning);
    }

    [Fact]
    public void Manual_selection_accepts_root_game_and_citadel_folders_and_rejects_others()
    {
        var service = CreateService();
        foreach (var folder in new[] { _installRoot, Path.Combine(_installRoot, "game"), Path.Combine(_installRoot, "game", "citadel"), _gameInfoPath })
            Assert.Equal(_gameInfoPath, service.FromUserSelection(folder, out _)?.GameInfoPath);

        Assert.Null(service.FromUserSelection(_root, out var error));
        Assert.Contains("gameinfo.gi", error);
    }

    [Fact]
    public void Steam_locator_finds_the_game_in_a_secondary_library()
    {
        var steamRoot = Path.Combine(_root, "Steam");
        var library = Path.Combine(_root, "Library2");
        Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));
        Directory.CreateDirectory(Path.Combine(library, "steamapps", "common", "Deadlock"));
        File.WriteAllText(Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"),
            $"\"libraryfolders\" {{ \"0\" {{ \"path\" \"{steamRoot.Replace("\\", "\\\\")}\" }} \"1\" {{ \"path\" \"{library.Replace("\\", "\\\\")}\" }} }}");
        File.WriteAllText(Path.Combine(library, "steamapps", "appmanifest_1422450.acf"),
            "\"AppState\" { \"appid\" \"1422450\" \"installdir\" \"Deadlock\" }");

        var found = new SteamLibraryLocator(() => steamRoot).FindAppInstallPath(DeadlockProfiles.SteamAppId);

        Assert.Equal(Path.Combine(library, "steamapps", "common", "Deadlock"), found);
    }

    [Fact]
    public void Every_profile_is_embedded_in_the_app_and_is_a_valid_gameinfo()
    {
        Assert.Equal(4, DeadlockProfiles.All.Count);
        foreach (var profile in DeadlockProfiles.All)
        {
            var text = GameTuningService.ValidateProfile(GameTuningService.LoadEmbedded(profile));
            Assert.Contains("citadel", text);
            Assert.Equal(12, profile.SourceCommit.Length);
        }
    }

    [Fact]
    public async Task Embedded_profile_applies_without_internet_and_undo_restores_the_original()
    {
        var service = new GameTuningService(_history, new SteamLibraryLocator(() => null), _stateDirectory, () => false, loadContent: null);
        var profile = DeadlockProfiles.GetById(DeadlockProfiles.LowEndId);

        var result = await service.ApplyAsync(Installation, service.Prepare(profile, Installation));

        Assert.Equal("Sucesso verificado", result.Outcome);
        Assert.Equal(GameTuningService.LoadEmbedded(profile), File.ReadAllBytes(_gameInfoPath));
        Assert.Equal("Reversão verificada", (await service.UndoAsync(Installation)).Outcome);
        Assert.Contains("\"original\"", File.ReadAllText(_gameInfoPath));
    }
}
