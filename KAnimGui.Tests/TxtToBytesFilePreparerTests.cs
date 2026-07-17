using KAnimGui.Infrastructure.Conversion;

namespace KAnimGui.Tests;

public sealed class TxtToBytesFilePreparerTests
{
    [Fact]
    public async Task Preparer_CopiesTxtToBytesWhenEnabled()
    {
        string root = Path.Combine(Path.GetTempPath(), "KAnimGuiTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string txtPath = Path.Combine(root, "hero_anim.txt");
            await File.WriteAllTextAsync(txtPath, "payload");
            var preparer = new TxtToBytesFilePreparer();

            string result = await preparer.PrepareBytesAsync(txtPath, true);

            Assert.Equal(Path.Combine(root, "hero_anim.bytes"), result);
            Assert.Equal("payload", await File.ReadAllTextAsync(result));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task Preparer_RejectsTxtWhenDisabled()
    {
        string path = Path.Combine(Path.GetTempPath(), "hero_anim.txt");
        var preparer = new TxtToBytesFilePreparer();

        await Assert.ThrowsAsync<InvalidOperationException>(() => preparer.PrepareBytesAsync(path, false));
    }
}
