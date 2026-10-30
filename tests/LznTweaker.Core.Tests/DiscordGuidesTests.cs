using LznTweaker.Core.Guides;

namespace LznTweaker.Core.Tests;

public sealed class DiscordGuidesTests
{
    [Fact]
    public void Guides_have_unique_ids_and_complete_instructions()
    {
        Assert.NotEmpty(DiscordGuides.All);
        Assert.Equal(DiscordGuides.All.Count, DiscordGuides.All.Select(g => g.Id).Distinct().Count());
        foreach (var guide in DiscordGuides.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(guide.Title), guide.Id);
            Assert.False(string.IsNullOrWhiteSpace(guide.Benefit), guide.Id);
            Assert.True(guide.Path.Count >= 2, guide.Id);
            Assert.True(guide.Steps.Count >= 2, guide.Id);
            Assert.True(Uri.TryCreate(guide.SourceUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps, guide.Id);
        }
    }

    [Fact]
    public void Every_guide_starts_from_user_settings()
    {
        foreach (var guide in DiscordGuides.All)
            Assert.Equal("Configurações de usuário", guide.Path[0]);
    }

    [Fact]
    public void Hardware_acceleration_is_not_duplicated()
    {
        // A opção em Aparência é a mesma de Avançado: o card dela só pede para conferir, nunca para desativar de novo.
        var appearance = DiscordGuides.All.Single(g => g.Id == "discord-appearance");
        Assert.Equal(GuideRecommendation.Check, appearance.Recommendation);
        Assert.Single(DiscordGuides.All, g => g.Recommendation == GuideRecommendation.Disable && g.Title == "Aceleração de hardware");
    }

    [Fact]
    public void Guides_make_no_performance_promises()
    {
        string[] forbidden = ["FPS garantido", "aumenta o FPS", "mais FPS", "garante"];
        foreach (var guide in DiscordGuides.All)
        {
            var text = string.Join(' ', [guide.Title, guide.Benefit, guide.Note ?? "", .. guide.Steps]);
            Assert.DoesNotContain(forbidden, word => text.Contains(word, StringComparison.CurrentCultureIgnoreCase));
        }
    }
}
