using KAnimGui.Application.Conversion;
using KAnimGui.Infrastructure.Conversion;

namespace KAnimGui.Tests;

public sealed class FileOperationLogSinkTests
{
    [Fact]
    public void Append_WritesStructuredOperationMessages()
    {
        string root = Path.Combine(Path.GetTempPath(), "KAnimGuiTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var sink = new FileOperationLogSink(root);
            sink.Append(new OperationEvent("kanim", "开始转换", false));
            sink.Append(new OperationEvent("kanim", "转换失败", true));

            string content = File.ReadAllText(sink.LogFilePath);
            Assert.Contains("[INFO] 开始转换", content);
            Assert.Contains("[ERROR] 转换失败", content);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
