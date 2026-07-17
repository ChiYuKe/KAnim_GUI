using KAnimGui.Application.Conversion;

namespace KAnimGui.Infrastructure.Conversion;

public sealed class TxtToBytesFilePreparer : IInputFilePreparer
{
    public Task<string> PrepareBytesAsync(
        string path,
        bool allowTxt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(path);
        }

        if (!allowTxt)
        {
            throw new InvalidOperationException("当前设置禁止使用 .txt 输入文件，请先在设置中启用 .txt 支持。");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("找不到输入文件。", path);
        }

        string bytesPath = Path.ChangeExtension(path, ".bytes");
        File.Copy(path, bytesPath, true);
        return Task.FromResult(bytesPath);
    }
}
