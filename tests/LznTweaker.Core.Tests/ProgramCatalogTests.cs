using LznTweaker.Core.Programs;

namespace LznTweaker.Core.Tests;

public sealed class ProgramCatalogTests
{
    private static readonly string[] InstallerExtensions = [".exe", ".zip", ".msi", ".7z", ".rar", ".msix", ".appx"];

    [Fact]
    public void Catalog_has_the_eight_programs_with_unique_ids()
    {
        Assert.Equal(8, ProgramCatalog.All.Count);
        Assert.Equal(ProgramCatalog.All.Count, ProgramCatalog.All.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void Every_link_is_https_and_never_a_direct_installer()
    {
        var urls = ProgramCatalog.All.SelectMany(p => p.Sources.Select(s => s.Url).Append(p.OfficialUrl));
        foreach (var url in urls)
        {
            Assert.True(Uri.TryCreate(url, UriKind.Absolute, out var uri), url);
            Assert.Equal(Uri.UriSchemeHttps, uri!.Scheme);
            var path = uri.AbsolutePath.ToLowerInvariant();
            Assert.DoesNotContain(InstallerExtensions, extension => path.EndsWith(extension, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Every_program_explains_itself_and_cites_sources()
    {
        foreach (var program in ProgramCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(program.Summary), program.Id);
            Assert.False(string.IsNullOrWhiteSpace(program.Purpose), program.Id);
            Assert.False(string.IsNullOrWhiteSpace(program.Benefits), program.Id);
            Assert.False(string.IsNullOrWhiteSpace(program.Limitations), program.Id);
            Assert.False(string.IsNullOrWhiteSpace(program.FutureIntegration), program.Id);
            Assert.NotEmpty(program.Features);
            Assert.NotEmpty(program.Tags);
            Assert.NotEmpty(program.Cautions);
            Assert.NotEmpty(program.Sources);
        }
    }

    [Fact]
    public void Texts_never_promise_gains()
    {
        var text = string.Join(" ", ProgramCatalog.All.SelectMany(p =>
            new[] { p.Summary, p.Purpose, p.Benefits }.Concat(p.Features).Concat(p.Steps).Concat(p.Settings.Select(s => s.Note ?? ""))));
        foreach (var forbidden in new[] { "garante", "garantido", "aumenta o FPS", "remove o input lag", "sem risco" })
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Every_filter_has_at_least_one_program()
    {
        foreach (var tag in Enum.GetValues<ProgramTag>())
            Assert.Contains(ProgramCatalog.All, program => program.Tags.Contains(tag));
    }
}
