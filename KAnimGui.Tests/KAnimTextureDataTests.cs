using KAnimGui.Core.Kanim;

namespace KAnimGui.Tests;

public sealed class KAnimTextureDataTests
{
    [Fact]
    public void TextureData_CopiesPngPayloadAndPreservesDimensions()
    {
        byte[] bytes = { 1, 2, 3 };
        var texture = new KAnimTextureData(2, 3, bytes);
        bytes[0] = 9;

        Assert.Equal(2, texture.Width);
        Assert.Equal(3, texture.Height);
        Assert.Equal(new byte[] { 1, 2, 3 }, texture.PngBytes);
    }
}
