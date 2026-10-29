using System.IO;
using System.Text.Json;
using LznTweaker.Core.Debloat;
using LznTweaker.Core.Execution;
using LznTweaker.Core.Tweaks;

namespace LznTweaker.Core.Tests;

public sealed class AppxCatalogTests
{
    [Theory]
    [InlineData("Microsoft.VCLibs.140.00", "Store", true, false, false)]
    [InlineData("Microsoft.Something.Resources", "Store", false, true, false)]
    [InlineData("Microsoft.Windows.ShellExperienceHost", "System", false, false, true)]
    [InlineData("Microsoft.LanguageExperiencePackpt-BR", "Store", false, false, false)]
    [InlineData("Microsoft.Ink.Handwriting.Main.pt-BR.1.0", "Store", false, false, false)]
    [InlineData("Microsoft.WindowsStore", "Store", false, false, false)]
    [InlineData("Microsoft.XboxIdentityProvider", "Store", false, false, false)]
    [InlineData("MicrosoftCorporationII.WinAppRuntime.Main.1.8", "Store", false, false, false)]
    [InlineData("Microsoft.VisualStudioCode", "Developer", false, false, false)]
    [InlineData("Microsoft.MicrosoftEdge.Stable", "Developer", false, false, false)]
    public void Protected_packages_are_never_offered(string name, string signature, bool framework, bool resource, bool nonRemovable) =>
        Assert.True(AppxCatalog.IsProtected(name, signature, framework, resource, nonRemovable));

    [Theory]
    [InlineData("MicrosoftCorporationII.QuickAssist", "Developer")]
    [InlineData("king.com.CandyCrushSaga", "Store")]
    [InlineData("Microsoft.WindowsCalculator", "Store")]
    public void Regular_and_curated_developer_packages_are_offered(string name, string signature) =>
        Assert.False(AppxCatalog.IsProtected(name, signature, false, false, false));

    [Theory]
    [InlineData("king.com.CandyCrushSaga", "CN=king.com", AppxCategory.Sponsored)]
    [InlineData("BytedancePte.Ltd.TikTok", "CN=Bytedance", AppxCategory.Sponsored)]
    [InlineData("Microsoft.BingNews", "CN=Microsoft Corporation", AppxCategory.Sponsored)]
    [InlineData("Microsoft.WindowsCalculator", "CN=Microsoft Corporation", AppxCategory.System)]
    [InlineData("Microsoft.XboxGamingOverlay", "CN=Microsoft Corporation", AppxCategory.System)]
    [InlineData("Microsoft.SomeNewInboxApp", "CN=Microsoft Corporation", AppxCategory.System)]
    [InlineData("SpotifyAB.SpotifyMusic", "CN=453637B3", AppxCategory.UserInstalled)]
    [InlineData("TelegramMessengerLLP.TelegramDesktop", "CN=536BC709", AppxCategory.UserInstalled)]
    public void Packages_are_classified(string name, string publisher, AppxCategory expected) =>
        Assert.Equal(expected, AppxCatalog.Classify(name, publisher).Category);

    [Fact]
    public void Unknown_names_are_made_readable() =>
        Assert.Equal("Wolfenstein TNO-PC", AppxCatalog.Prettify("BethesdaSoftworks.WolfensteinTNO-PC"));

    [Fact]
    public void Store_uri_only_accepts_package_family_names()
    {
        Assert.Equal("ms-windows-store://pdp/?PFN=Microsoft.WindowsCalculator_8wekyb3d8bbwe", AppxDebloatService.GetStoreUri("Microsoft.WindowsCalculator_8wekyb3d8bbwe"));
        Assert.Equal("ms-windows-store://home", AppxDebloatService.GetStoreUri("x&y=1 cmd"));
    }
}

public sealed class AppxDebloatServiceTests : IDisposable
{
    private readonly string _historyPath = Path.Combine(Path.GetTempPath(), $"LznTweakerTest_{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_historyPath);

    private AppxDebloatService CreateService(out TweakOperationHistory history)
    {
        history = new TweakOperationHistory(_historyPath);
        return new AppxDebloatService(BatchEngineHarness.CreateRunner(), history);
    }

    [Fact]
    public void Inventory_hides_protected_packages_merges_architectures_and_warns_about_other_account()
    {
        var json = JsonSerializer.Serialize(new
        {
            CurrentUserSid = "S-1-5-21-1-500",
            InteractiveUserSid = "S-1-5-21-1-1001",
            ProvisionedKnown = true,
            Packages = new object[]
            {
                Raw("king.com.CandyCrushSaga", "CN=king.com", "Store", provisioned: true),
                Raw("Microsoft.WindowsCalculator", "CN=Microsoft Corporation", "Store", arch: "x64"),
                Raw("Microsoft.WindowsCalculator", "CN=Microsoft Corporation", "Store", arch: "x86"),
                Raw("Microsoft.VCLibs.140.00", "CN=Microsoft Corporation", "Store", framework: true),
                Raw("Microsoft.MicrosoftEdge.Stable", "CN=Microsoft Corporation", "Developer"),
                Raw("Microsoft.Windows.ShellExperienceHost", "CN=Microsoft Windows", "System", nonRemovable: true)
            }
        });

        var inventory = AppxDebloatService.ParseInventory(json);

        Assert.Equal(["Calculadora", "Candy Crush Saga"], inventory.Packages.Select(package => package.FriendlyName));
        Assert.Equal(3, inventory.ProtectedCount);
        Assert.NotNull(inventory.AccountWarning);
        Assert.True(inventory.Packages.Single(package => package.Name.StartsWith("king")).IsProvisioned);
    }

    [Fact]
    public async Task Restorable_list_follows_the_history()
    {
        var service = CreateService(out var history);
        await history.AppendAsync(Removal("king.com.CandyCrushSaga", "Candy Crush Saga", filesKept: false));
        await history.AppendAsync(Removal("Microsoft.WindowsCalculator", "Calculadora", filesKept: true));
        await history.AppendAsync(Removal("Microsoft.BingNews", "Notícias", filesKept: true));
        await history.AppendAsync(Restore("Microsoft.BingNews", "Reversão verificada"));
        await history.AppendAsync(Restore("king.com.CandyCrushSaga", "Requer Microsoft Store"));
        // Dados de reversão que não correspondem ao TweakId são ignorados.
        await history.AppendAsync(Removal("Microsoft.Evil", "Falso", filesKept: true) with { TweakId = "appx:Microsoft.Other" });

        var restorable = await service.GetRestorableAsync();

        Assert.Equal(["Calculadora", "Candy Crush Saga"], restorable.Select(item => item.FriendlyName).Order());
        Assert.Contains("PFN=king.com.CandyCrushSaga_kgqvnymyfvs32", restorable.Single(item => item.Name.StartsWith("king")).StoreUri);
    }

    [NonElevatedFact]
    public async Task Removal_is_blocked_when_the_restore_point_cannot_be_created()
    {
        var service = CreateService(out var history);
        var package = new AppxPackageInfo("LznTweaker.NoSuchPackage", "Pacote inexistente", "x", "x_1234567890abc", "CN=Test", "1.0",
            AppxCategory.Sponsored, null, null);

        var results = await service.RemoveAsync([package], allUsers: false);

        Assert.Equal(TweakCatalog.CreateRestorePointId, results[0].TweakId);
        Assert.Equal("Falha ou não verificado", results[0].Outcome);
        Assert.Equal("Bloqueado", results[1].Outcome);
        Assert.Equal(2, (await history.ReadAsync()).Count);
    }

    [Fact]
    public void Engine_reports_missing_package_and_store_only_restore()
    {
        var restoreData = JsonSerializer.Serialize(new
        {
            Name = "LznTweaker.NoSuchPackage", PackageFullName = "x", PackageFamilyName = "LznTweaker.NoSuchPackage_1234567890abc",
            InstallLocation = @"C:\LznTweakerNoSuchFolder", ManifestPath = @"C:\LznTweakerNoSuchFolder\AppxManifest.xml", AllUsers = false, FilesKeptAfterRemoval = false
        });
        var results = BatchEngineHarness.Run(new object[]
        {
            new { TweakId = "appx:missing", Name = "missing", Kind = "RemoveAppx", PackageName = "LznTweaker.NoSuchPackage", AllUsers = false },
            new { TweakId = "appx:restore", Name = "restore", Kind = "RestoreAppx", RollbackData = restoreData }
        });

        Assert.Equal("Nada a alterar", (string?)results[0]!["Outcome"]);
        Assert.Equal("Requer Microsoft Store", (string?)results[1]!["Outcome"]);
    }

    [NonElevatedFact]
    public void Engine_refuses_packages_protected_by_windows()
    {
        // NonRemovable: o motor recusa antes de chamar Remove-AppxPackage (e o próprio Windows também recusaria).
        var inventory = AppxDebloatService.ParseInventory(RunInventory());
        var results = BatchEngineHarness.Run(new[]
        {
            new { TweakId = "appx:shell", Name = "shell", Kind = "RemoveAppx", PackageName = "Microsoft.Windows.ShellExperienceHost", AllUsers = false }
        });

        Assert.Equal("Falha ou não verificado", (string?)results[0]!["Outcome"]);
        Assert.Contains("protegido", (string?)results[0]!["Message"]);
        Assert.DoesNotContain(inventory.Packages, package => package.Name == "Microsoft.Windows.ShellExperienceHost");
    }

    [Fact]
    public void Real_inventory_never_lists_protected_packages()
    {
        var inventory = AppxDebloatService.ParseInventory(RunInventory());

        Assert.NotEmpty(inventory.Packages);
        Assert.True(inventory.ProtectedCount > 0);
        Assert.DoesNotContain(inventory.Packages, package =>
            package.Name.StartsWith("Microsoft.VCLibs", StringComparison.OrdinalIgnoreCase) ||
            package.Name.StartsWith("Microsoft.LanguageExperiencePack", StringComparison.OrdinalIgnoreCase) ||
            package.Name is "Microsoft.WindowsStore" or "Microsoft.MicrosoftEdge.Stable" or "Microsoft.XboxIdentityProvider");
        Assert.Equal(inventory.Packages.Count, inventory.Packages.Select(package => package.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private static string RunInventory()
    {
        var run = BatchEngineHarness.CreateRunner()
            .RunAsync(new ScriptRunRequest("inventory", EmbeddedScripts.AppxInventory, "{}", TimeSpan.FromMinutes(2), RequiresElevation: false))
            .GetAwaiter().GetResult();
        Assert.True(run.ResultJson is not null, $"{run.Message} {run.ErrorOutput}");
        return run.ResultJson!;
    }

    private static object Raw(string name, string publisher, string signature, bool framework = false, bool nonRemovable = false,
        bool? provisioned = null, string arch = "x64") => new
    {
        Name = name, PackageFullName = $"{name}_1.0.0.0_{arch}__abc", PackageFamilyName = $"{name}_1234567890abc", Publisher = publisher,
        Version = "1.0.0.0", InstallLocation = @"C:\x", SignatureKind = signature, IsFramework = framework, IsResourcePackage = false,
        NonRemovable = nonRemovable, IsProvisioned = provisioned
    };

    private static TweakOperationResult Removal(string name, string friendly, bool filesKept) => new(
        Guid.NewGuid(), AppxDebloatService.OperationPrefix + name, DateTimeOffset.Now, "Instalado", $"Remover {friendly}", "Sucesso verificado", "",
        false, "Re-registrar", null,
        JsonSerializer.Serialize(new { Name = name, PackageFamilyName = $"{name}_kgqvnymyfvs32", AllUsers = false, FilesKeptAfterRemoval = filesKept }));

    private static TweakOperationResult Restore(string name, string outcome) => new(
        Guid.NewGuid(), AppxDebloatService.OperationPrefix + name, DateTimeOffset.Now, "Removido", "Restaurar app", outcome, "", false, null, null);
}
