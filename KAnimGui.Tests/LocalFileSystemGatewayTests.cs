using KAnimGui.Infrastructure.Platform;

namespace KAnimGui.Tests;

public sealed class LocalFileSystemGatewayTests
{
    [Fact]
    public void TryEnsureWritableDirectory_CreatesAndProbesDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "KAnimGuiTests", Guid.NewGuid().ToString("N"));
        try
        {
            var gateway = new LocalFileSystemGateway();

            bool result = gateway.TryEnsureWritableDirectory(root, out var error);

            Assert.True(result);
            Assert.Null(error);
            Assert.True(Directory.Exists(root));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
