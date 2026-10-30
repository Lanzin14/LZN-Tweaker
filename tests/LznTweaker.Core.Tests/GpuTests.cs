using System.IO;
using LznTweaker.Core.GameOptimization;
using LznTweaker.Core.Gpu;

namespace LznTweaker.Core.Tests;

public sealed class GpuTests
{
    private static Dictionary<string, string?> AdapterKey(string name, string? deviceId, string? provider = null, string? version = null, string? radeon = null, string? date = null) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["DriverDesc"] = name,
            ["MatchingDeviceId"] = deviceId,
            ["ProviderName"] = provider,
            ["DriverVersion"] = version,
            ["RadeonSoftwareVersion"] = radeon,
            ["DriverDate"] = date
        };

    [Theory]
    [InlineData(@"PCI\VEN_10DE&DEV_2684", null, GpuVendor.Nvidia)]
    [InlineData(@"pci\ven_1002&dev_73ff", null, GpuVendor.Amd)]
    [InlineData(@"PCI\VEN_8086&DEV_A780", null, GpuVendor.Intel)]
    [InlineData(null, "Advanced Micro Devices, Inc.", GpuVendor.Amd)]
    [InlineData(null, "NVIDIA", GpuVendor.Nvidia)]
    [InlineData(null, null, GpuVendor.Unknown)]
    public void Vendor_comes_from_pci_id_then_provider(string? deviceId, string? provider, GpuVendor expected) =>
        Assert.Equal(expected, GpuDetectionService.ParseVendor(deviceId, provider));

    [Theory]
    [InlineData("32.0.15.6094", "560.94")]
    [InlineData("31.0.15.5222", "552.22")]
    [InlineData("32.0.15.0612", "506.12")]
    [InlineData("32.0.x.6094", null)]
    [InlineData("32.0.15", null)]
    [InlineData(null, null)]
    public void Nvidia_version_is_derived_only_from_the_expected_format(string? windows, string? expected) =>
        Assert.Equal(expected, GpuDetectionService.NvidiaDriverVersion(windows));

    [Fact]
    public void Detection_prefers_dedicated_gpu_and_skips_basic_adapter()
    {
        var service = new GpuDetectionService(() =>
        [
            AdapterKey("Microsoft Basic Display Adapter", null),
            AdapterKey("Intel(R) UHD Graphics 770", @"PCI\VEN_8086&DEV_4680", version: "31.0.101.4502"),
            AdapterKey("AMD Radeon RX 6600", @"PCI\VEN_1002&DEV_73FF", "Advanced Micro Devices, Inc.", "32.0.21045.16010", "26.10.1", "10-16-2026")
        ], () => 240);

        var result = service.Detect();

        Assert.Equal(GpuDetectionStatus.Available, result.Status);
        Assert.Equal(2, result.Adapters.Count);
        var primary = Assert.IsType<GpuAdapter>(result.Primary);
        Assert.Equal("AMD Radeon RX 6600", primary.Name);
        Assert.Equal(GpuVendor.Amd, primary.Vendor);
        Assert.Equal("26.10.1", primary.VendorSoftwareVersion);
        Assert.Equal(new DateOnly(2026, 10, 16), primary.DriverDate);
        Assert.Equal(240, result.RefreshRateHz);
    }

    [Fact]
    public void Missing_data_is_reported_as_unavailable_not_invented()
    {
        var empty = new GpuDetectionService(() => [AdapterKey("Microsoft Basic Render Driver", null)], () => null).Detect();
        Assert.Equal(GpuDetectionStatus.Unavailable, empty.Status);
        Assert.Null(empty.Primary);
        Assert.Null(empty.RefreshRateHz);

        var failing = new GpuDetectionService(() => throw new UnauthorizedAccessException("negado"), () => throw new InvalidOperationException()).Detect();
        Assert.Equal(GpuDetectionStatus.Unavailable, failing.Status);
        Assert.Equal("negado", failing.Error);

        var noVersion = GpuDetectionService.ToAdapter(AdapterKey("NVIDIA GeForce RTX 4070", @"PCI\VEN_10DE&DEV_2786", version: "corrompida"));
        Assert.NotNull(noVersion);
        Assert.Null(noVersion!.VendorSoftwareVersion);
        Assert.Null(noVersion.DriverDate);
    }

    [Fact]
    public void Catalog_is_complete_and_never_claims_automatic_application()
    {
        Assert.Equal(GpuCatalog.DriverSettings.Count, GpuCatalog.DriverSettings.Select(s => s.Id).Distinct().Count());
        Assert.Contains(GpuCatalog.DriverSettings, s => s.Vendor == GpuVendor.Nvidia);
        Assert.Contains(GpuCatalog.DriverSettings, s => s.Vendor == GpuVendor.Amd);
        foreach (var setting in GpuCatalog.DriverSettings)
        {
            // Sem integração NVAPI/ADLX, nenhum ajuste pode se apresentar como automático.
            Assert.Equal(GpuApplyMethod.Manual, setting.Method);
            Assert.NotEmpty(setting.Options);
            Assert.False(string.IsNullOrWhiteSpace(setting.ManualPath));
            var guide = setting.Guide;
            Assert.All(new[] { guide.WhatItDoes, guide.Benefit, guide.Downsides, guide.WhenToUse, guide.Compatibility, guide.HowToRestore },
                text => Assert.False(string.IsNullOrWhiteSpace(text), setting.Id));
        }
        // Anti-Lag tradicional e Anti-Lag 2 são itens distintos.
        Assert.Contains(GpuCatalog.DriverSettings, s => s.Id == "amd-antilag");
        Assert.Contains(GpuCatalog.DriverSettings, s => s.Id == "amd-antilag2");
    }

    [Fact]
    public void Game_profiles_match_supported_games_and_avoid_fps_promises()
    {
        Assert.Equal(SupportedGames.All.Select(g => g.Id).Order(), GpuCatalog.Games.Select(g => g.GameId).Order());
        foreach (var game in GpuCatalog.Games)
        {
            Assert.Equal(SupportedGames.All.First(g => g.Id == game.GameId).SteamAppId, game.SteamAppId);
            Assert.Contains(game.VendorAdvice, a => a.Vendor == GpuVendor.Nvidia);
            Assert.Contains(game.VendorAdvice, a => a.Vendor == GpuVendor.Amd);
            Assert.NotEmpty(game.Settings);
            Assert.Equal(game.Settings.Count, game.Settings.Select(s => s.Name).Distinct().Count());
        }

        var allText = string.Join(" ", GpuCatalog.Games.SelectMany(g => g.Settings.Select(s => s.Note + s.VisibilityNote).Append(g.TestAdvice).Concat(g.VendorAdvice.Select(a => a.Detail)))
            .Concat(GpuCatalog.DriverSettings.Select(s => s.Guide.Benefit)));
        Assert.DoesNotContain("garante", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("remove todo", allText, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(VrrAnswer.Available, 240, "237")]
    [InlineData(VrrAnswer.NotAvailable, 240, "Sem VRR")]
    [InlineData(VrrAnswer.NotAnswered, null, "a taxa do seu monitor")]
    public void Cap_advice_follows_vrr_answer_and_refresh_rate(VrrAnswer vrr, int? hz, string expected) =>
        Assert.Contains(expected, GpuCatalog.CapAdvice(vrr, hz));

    [Fact]
    public void Preferences_round_trip_and_survive_a_corrupt_file()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"LznTweakerTest_{Guid.NewGuid():N}");
        try
        {
            var store = new GpuPreferencesStore(Path.Combine(folder, "gpu.json"));
            Assert.Equal(GpuOptimizationMode.CompetitiveLatency, store.Load().Mode);

            var preferences = new GpuPreferences { Mode = GpuOptimizationMode.Custom, Vrr = VrrAnswer.Available };
            preferences.CustomValues["nv-llm"] = "On";
            store.Save(preferences);

            var loaded = store.Load();
            Assert.Equal(GpuOptimizationMode.Custom, loaded.Mode);
            Assert.Equal(VrrAnswer.Available, loaded.Vrr);
            Assert.Equal("On", loaded.CustomValues["nv-llm"]);

            File.WriteAllText(store.FilePath, "{ corrompido");
            Assert.Equal(GpuOptimizationMode.CompetitiveLatency, store.Load().Mode);
            Assert.True(File.Exists(store.FilePath + ".invalido"));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }
}
