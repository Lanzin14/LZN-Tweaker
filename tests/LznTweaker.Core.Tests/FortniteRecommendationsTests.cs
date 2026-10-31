using LznTweaker.Core.GameOptimization;

namespace LznTweaker.Core.Tests;

public sealed class FortniteRecommendationsTests
{
    private static IEnumerable<RecommendedSetting> AllSettings =>
        FortniteRecommendations.PerformanceMode.Concat(FortniteRecommendations.DirectX12)
            .Concat(FortniteRecommendations.Nvidia).Concat(FortniteRecommendations.Amd);

    [Fact]
    public void Every_setting_is_complete_and_unique_per_section()
    {
        foreach (var section in new[] { FortniteRecommendations.PerformanceMode, FortniteRecommendations.DirectX12, FortniteRecommendations.Nvidia, FortniteRecommendations.Amd })
        {
            Assert.NotEmpty(section);
            Assert.Equal(section.Count, section.Select(setting => setting.Name).Distinct().Count());
            Assert.All(section, setting =>
            {
                Assert.False(string.IsNullOrWhiteSpace(setting.Value), setting.Name);
                Assert.False(string.IsNullOrWhiteSpace(setting.Purpose), setting.Name);
            });
        }
    }

    [Fact]
    public void Nothing_promises_fps_or_ping()
    {
        var text = string.Join(' ', AllSettings.SelectMany(setting => new[] { setting.Purpose, setting.Note ?? "" })
            .Concat(FortniteRecommendations.VideoNotes).Concat(FortniteRecommendations.GpuNotes)
            .Concat(GuidanceCatalog.All.Where(item => item.Area == OptimizationArea.Fortnite).SelectMany(item => item.Steps.Append(item.Summary))));
        string[] forbidden = ["garante", "garantido", "mais FPS", "aumenta o FPS", "reduz o ping"];
        Assert.DoesNotContain(forbidden, word => text.Contains(word, StringComparison.CurrentCultureIgnoreCase));
    }

    [Fact]
    public void Amd_never_recommends_anti_lag_2_as_enabled_and_chill_stays_off()
    {
        Assert.DoesNotContain(FortniteRecommendations.Amd, setting => setting.Name.Contains("Anti-Lag 2", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Desativado", FortniteRecommendations.Amd.Single(setting => setting.Name == "Radeon Chill").Value);
    }

    [Fact]
    public void Nvidia_low_latency_mode_is_not_stacked_on_top_of_reflex()
    {
        Assert.Equal("Desligado", FortniteRecommendations.Nvidia.Single(setting => setting.Name == "Modo de baixa latência").Value);
    }

    [Fact]
    public void Process_priority_is_refused_and_anticheat_is_never_touched()
    {
        var fortnite = GuidanceCatalog.All.Where(item => item.Area == OptimizationArea.Fortnite).ToList();
        Assert.True(fortnite.Single(item => item.Id == "g-fortnite-priority").Refused);
        Assert.Contains(fortnite, item => item.Id == "g-fortnite-anticheat");
        Assert.DoesNotContain(fortnite.SelectMany(item => item.Steps), step => step.Contains("Tempo real", StringComparison.Ordinal) && !step.Contains("Nunca", StringComparison.Ordinal));
    }

    [Fact]
    public void Launch_arguments_match_the_epic_help_page()
    {
        var steps = string.Join(' ', GuidanceCatalog.All.Single(item => item.Id == "g-fortnite-launch-args").Steps);
        Assert.Contains("-d3d12 -FeatureLevelEs31", steps);
        Assert.Contains("-d3d12 -sm6", steps);
    }
}
