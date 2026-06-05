using KAnimGui.Core;

namespace KAnimGui.Tests;

public sealed class ConverterArgumentTests
{
    [Fact]
    public void KanimConverter_BuildArguments_UsesDefaultAnimThenBuildOrder()
    {
        var converter = new KanimConverter
        {
            PngPath = @"C:\input\plant.png",
            AnimPath = @"C:\input\plant_anim.bytes",
            BuildPath = @"C:\input\plant_build.bytes",
            StrictOrder = false,
            StrictMode = false
        };

        var args = converter.BuildArguments(@"C:\output\plant");

        Assert.Equal(
            new[]
            {
                "scml",
                @"C:\input\plant.png",
                @"C:\input\plant_anim.bytes",
                @"C:\input\plant_build.bytes",
                "-o",
                @"C:\output\plant"
            },
            args);
    }

    [Fact]
    public void KanimConverter_BuildArguments_UsesStrictBuildThenAnimOrder()
    {
        var converter = new KanimConverter
        {
            PngPath = @"C:\input\plant.png",
            AnimPath = @"C:\input\plant_anim.bytes",
            BuildPath = @"C:\input\plant_build.bytes",
            StrictOrder = true,
            StrictMode = true
        };

        var args = converter.BuildArguments(@"C:\output\plant");

        Assert.Equal(
            new[]
            {
                "scml",
                @"C:\input\plant.png",
                "-f",
                @"C:\input\plant_build.bytes",
                @"C:\input\plant_anim.bytes",
                "-o",
                @"C:\output\plant",
                "-S"
            },
            args);
    }

    [Fact]
    public void ScmlConverter_BuildArguments_AddsOptionalFlags()
    {
        var converter = new ScmlConverter
        {
            ScmlPath = @"C:\input\plant.scml",
            Interpolate = true,
            Debone = true
        };

        var args = converter.BuildArguments(@"C:\output\plant");

        Assert.Equal(
            new[]
            {
                "kanim",
                @"C:\input\plant.scml",
                "-o",
                @"C:\output\plant",
                "-i",
                "-b"
            },
            args);
    }
}
