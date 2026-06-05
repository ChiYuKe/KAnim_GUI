using KAnimGui.Core;

namespace KAnimGui.Tests;

public sealed class KanimFileMatcherTests
{
    [Theory]
    [InlineData("duplicant", "duplicant")]
    [InlineData("duplicant_0", "duplicant")]
    [InlineData("duplicant_12", "duplicant")]
    [InlineData("duplicant_front", "duplicant_front")]
    [InlineData("duplicant_", "duplicant_")]
    public void NormalizePngBaseName_StripsOnlyTrailingNumericSuffix(string input, string expected)
    {
        Assert.Equal(expected, KanimFileMatcher.NormalizePngBaseName(input));
    }

    [Fact]
    public void ValidateFileSet_AcceptsNumericPngSuffix()
    {
        using var temp = new TempKanimFolder();
        var pngPath = temp.Touch("dupe_0.png");
        var animPath = temp.Touch("dupe_anim.bytes");
        var buildPath = temp.Touch("dupe_build.bytes");

        var result = KanimFileMatcher.ValidateFileSet(pngPath, animPath, buildPath, allowTxt: false);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateFileSet_RejectsMismatchedBaseName()
    {
        using var temp = new TempKanimFolder();
        var pngPath = temp.Touch("dupe_0.png");
        var animPath = temp.Touch("other_anim.bytes");
        var buildPath = temp.Touch("dupe_build.bytes");

        var result = KanimFileMatcher.ValidateFileSet(pngPath, animPath, buildPath, allowTxt: false);

        Assert.False(result.IsValid);
        Assert.Contains("基础文件名不一致", result.ErrorMessage);
    }

    [Fact]
    public void FindFileSets_PairsNumericPngSuffixWithMatchingAnimAndBuild()
    {
        using var temp = new TempKanimFolder();
        var pngPath = temp.Touch("plant_0.png");
        var animPath = temp.Touch("plant_anim.bytes");
        var buildPath = temp.Touch("plant_build.bytes");

        var fileSet = Assert.Single(KanimFileMatcher.FindFileSets(temp.Path, allowTxt: false));

        Assert.Equal("plant", fileSet.Name);
        Assert.Equal(pngPath, fileSet.PngPath);
        Assert.Equal(animPath, fileSet.AnimPath);
        Assert.Equal(buildPath, fileSet.BuildPath);
    }

    [Fact]
    public void FindFileSets_PrefersBytesOverTxtWhenBothExist()
    {
        using var temp = new TempKanimFolder();
        temp.Touch("plant.png");
        var animBytesPath = temp.Touch("plant_anim.bytes");
        var buildBytesPath = temp.Touch("plant_build.bytes");
        temp.Touch("plant_anim.txt");
        temp.Touch("plant_build.txt");

        var fileSet = Assert.Single(KanimFileMatcher.FindFileSets(temp.Path, allowTxt: true));

        Assert.Equal(animBytesPath, fileSet.AnimPath);
        Assert.Equal(buildBytesPath, fileSet.BuildPath);
    }

    [Fact]
    public void FindFileSets_IgnoresTxtWhenTxtSupportIsDisabled()
    {
        using var temp = new TempKanimFolder();
        temp.Touch("plant.png");
        temp.Touch("plant_anim.txt");
        temp.Touch("plant_build.txt");

        Assert.Empty(KanimFileMatcher.FindFileSets(temp.Path, allowTxt: false));
    }

    private sealed class TempKanimFolder : IDisposable
    {
        public TempKanimFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"KAnimGuiTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string Touch(string fileName)
        {
            var path = System.IO.Path.Combine(Path, fileName);
            File.WriteAllText(path, string.Empty);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
