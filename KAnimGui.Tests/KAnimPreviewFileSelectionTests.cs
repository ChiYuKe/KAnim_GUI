using KAnimGui.Presentation.Preview;

namespace KAnimGui.Tests;

public sealed class KAnimPreviewFileSelectionTests
{
    [Fact]
    public void AddFiles_ClassifiesAndMergesPreviewPackageFiles()
    {
        var selection = KAnimPreviewFileSelection.Empty.AddFiles(new[]
        {
            @"C:\assets\hero_build.bytes",
            @"C:\assets\hero.png"
        });

        selection = selection.AddFiles(new[] { @"C:\assets\hero_anim.bytes" });

        Assert.True(selection.IsComplete);
        Assert.Equal("hero.png", selection.Entries[0].FileName);
        Assert.Equal("hero_anim.bytes", selection.Entries[1].FileName);
        Assert.Equal("hero_build.bytes", selection.Entries[2].FileName);
    }

    [Fact]
    public void AddFiles_IgnoresUnknownFiles()
    {
        var selection = KAnimPreviewFileSelection.Empty.AddFiles(new[] { @"C:\assets\readme.txt" });

        Assert.False(selection.HasAnyFile);
        Assert.Empty(selection.Entries);
    }
}
