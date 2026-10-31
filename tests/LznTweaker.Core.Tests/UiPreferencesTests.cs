using System.IO;
using LznTweaker.Core.Preferences;

namespace LznTweaker.Core.Tests;

public sealed class UiPreferencesTests
{
    [Fact]
    public void Sidebar_starts_collapsed_round_trips_and_survives_a_corrupt_file()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"LznTweakerTest_{Guid.NewGuid():N}");
        try
        {
            var store = new UiPreferencesStore(Path.Combine(folder, "ui.json"));
            Assert.True(store.Load().SidebarCollapsed);

            Assert.True(store.TrySave(new UiPreferences { SidebarCollapsed = false }));
            Assert.False(store.Load().SidebarCollapsed);

            File.WriteAllText(store.FilePath, "{ corrompido");
            Assert.True(store.Load().SidebarCollapsed);
            Assert.True(File.Exists(store.FilePath + ".invalido"));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }
}
