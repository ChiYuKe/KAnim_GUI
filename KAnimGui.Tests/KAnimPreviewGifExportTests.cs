using KAnimGui.Presentation.Preview;

public sealed class KAnimPreviewGifExportTests
{
    [Theory]
    [InlineData(25, 4)]
    [InlineData(30, 3)]
    [InlineData(100, 1)]
    public void GifOptions_ConvertsFrameRateToGifDelay(double fps, int expectedDelayCentiseconds)
    {
        var options = new KAnimGifExportOptions(fps, 768, 768);

        Assert.Equal(expectedDelayCentiseconds, options.DelayCentiseconds);
    }
}
