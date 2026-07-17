using KAnimGui.Application.Conversion;
using KAnimGui.Infrastructure.Conversion;

namespace KAnimGui.Tests;

public sealed class CliProcessRunnerServiceTests
{
    [Fact]
    public async Task Runner_ReturnsStructuredStartFailure()
    {
        var runner = new CliProcessRunnerService();
        var events = new List<OperationEvent>();

        ProcessExecutionResult result = await runner.RunAsync(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-cli.exe"),
            Array.Empty<string>(),
            new Progress<OperationEvent>(events.Add));

        Assert.False(result.Succeeded);
        Assert.False(result.WasCanceled);
        Assert.Contains("启动转换进程失败", result.ErrorMessage);
        Assert.Contains(events, item => item.IsError);
    }
}
