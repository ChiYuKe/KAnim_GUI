using KAnimGui.Application.Conversion;
using KAnimGui.Infrastructure.Conversion;

namespace KAnimGui.Tests;

public sealed class KseExecutableLocatorTests
{
    [Fact]
    public void Locator_PrefersExistingConfiguredPath()
    {
        string root = Path.Combine(Path.GetTempPath(), "KAnimGuiTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string configuredPath = Path.Combine(root, "kanimal-cli.exe");
        File.WriteAllBytes(configuredPath, new byte[] { 1 });
        try
        {
            var locator = new KseExecutableLocator(new FakeSettings(true, configuredPath));

            Assert.Equal(configuredPath, locator.FindExecutable());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private sealed record FakeSettings(bool UseCustomKsePath, string CustomKsePath) : IKsePathSettings;
}
