using KAnimGui.Presentation.Preview;

public sealed class KAnimPreviewGifExportTests
{
    [Theory]
    [InlineData(30, 0.5, 7)]
    [InlineData(30, 1.0, 3)]
    [InlineData(30, 2.0, 2)]
    [InlineData(60, 1.0, 2)]
    public void GifOptions_ConvertsPlaybackSpeedToGifDelay(
        double animationFps,
        double playbackSpeed,
        int expectedDelayCentiseconds)
    {
        var options = new KAnimGifExportOptions(playbackSpeed, 768, 768);

        Assert.Equal(expectedDelayCentiseconds, options.GetDelayCentiseconds(animationFps));
    }

    [Fact]
    public void GifOptions_ShowsCompletionNotificationByDefault()
    {
        var options = new KAnimGifExportOptions(1.0, 768, 768);

        Assert.True(options.ShowCompletionNotification);
        Assert.Equal(KAnimGifScalingMode.Lanczos, options.ScalingMode);
    }

    [Theory]
    [InlineData(30, 0.5, 15)]
    [InlineData(30, 1.0, 30)]
    [InlineData(60, 2.0, 100)]
    public void GifOptions_ClampsEffectiveFpsToGifTimingLimit(
        double animationFps,
        double playbackSpeed,
        double expectedFps)
    {
        var options = new KAnimGifExportOptions(playbackSpeed, 768, 768);

        Assert.Equal(expectedFps, options.GetEffectiveFramesPerSecond(animationFps));
    }

    [Fact]
    public void GifPath_BuildsKAnimAndAnimationFileName()
    {
        Assert.Equal(
            "anim_bionic_vomit_loop.gif",
            KAnimGifExportPathResolver.BuildGifFileName("anim_bionic", "vomit_loop"));
    }

    [Fact]
    public void GifPath_UsesKAnimSubdirectoryForBatchExports()
    {
        string root = Path.Combine(Path.GetTempPath(), "KAnimGuiTests");

        Assert.Equal(
            Path.Combine(root, "anim_bionic"),
            KAnimGifExportPathResolver.GetBatchExportDirectory(root, "anim_bionic"));
    }
}
