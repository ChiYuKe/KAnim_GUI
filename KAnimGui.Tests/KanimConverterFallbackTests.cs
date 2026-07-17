using KAnimGui.Application.Conversion;
using KAnimGui.Core;

namespace KAnimGui.Tests;

public sealed class KanimConverterFallbackTests
{
    [Fact]
    public async Task CliFailure_LogsAndAttemptsBuiltInExporter()
    {
        string root = Path.Combine(Path.GetTempPath(), "KAnimGuiTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var logs = new List<string>();
            var runner = new FailingProcessRunner();
            var converter = new KanimConverter
            {
                PngPath = Path.Combine(root, "hero.png"),
                AnimPath = Path.Combine(root, "hero_anim.bytes"),
                BuildPath = Path.Combine(root, "hero_build.bytes"),
                OutputDir = root,
                CliExecutablePath = "kanimal-cli-test-double",
                ProcessRunner = runner
            };

            var result = await converter.ConvertAsync((message, _) => logs.Add(message));

            Assert.True(runner.Called);
            Assert.True(result.Success);
            Assert.Contains(logs, message => message.Contains("改用内置", StringComparison.Ordinal));
            Assert.True(Directory.Exists(converter.ActualOutputDir));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private sealed class FailingProcessRunner : IProcessRunner
    {
        public bool Called { get; private set; }

        public Task<ProcessExecutionResult> RunAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            IProgress<OperationEvent>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            progress?.Report(new OperationEvent("test", "CLI 模拟失败", true, Stage: "process"));
            return Task.FromResult(new ProcessExecutionResult(false, 42, "退出代码: 42", false));
        }
    }
}
