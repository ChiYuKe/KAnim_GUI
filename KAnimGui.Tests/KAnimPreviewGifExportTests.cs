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
}
