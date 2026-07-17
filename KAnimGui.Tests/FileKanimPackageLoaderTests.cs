using KAnimGui.Infrastructure.Preview;

namespace KAnimGui.Tests;

public sealed class FileKanimPackageLoaderTests
{
    [Fact]
    public async Task Loader_ReadsPngBytesAndDimensionsWithoutWpfPackageModel()
    {
        string root = Path.Combine(Path.GetTempPath(), "KAnimGuiTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string texturePath = Path.Combine(root, "texture.png");
            await File.WriteAllBytesAsync(texturePath, Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));

            var loader = new FileKanimPackageLoader(new PngTextureCodec());
            var package = await loader.LoadAsync(
                texturePath,
                Path.Combine(root, "missing_build.bytes"),
                Path.Combine(root, "missing_anim.bytes"));

            Assert.Null(package.Build);
            Assert.Null(package.Anim);
            Assert.NotNull(package.Texture);
            Assert.Equal(1, package.Texture!.Width);
            Assert.Equal(1, package.Texture.Height);
            Assert.NotEmpty(package.Texture.PngBytes);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
