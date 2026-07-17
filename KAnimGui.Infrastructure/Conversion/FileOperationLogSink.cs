using System.Globalization;
using KAnimGui.Application.Conversion;

namespace KAnimGui.Infrastructure.Conversion;

public sealed class FileOperationLogSink : IOperationLogSink
{
    private readonly object sync = new();

    public FileOperationLogSink(string? logDirectory = null)
    {
        LogDirectory = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "KSE_Output",
            "Logs");
        Directory.CreateDirectory(LogDirectory);
        LogFilePath = Path.Combine(
            LogDirectory,
            $"KAnimGui_{DateTime.Now:yyyyMMdd_HHmmss}.log");
    }

    public string LogDirectory { get; }
    public string LogFilePath { get; }

    public void Append(OperationEvent operationEvent)
    {
        ArgumentNullException.ThrowIfNull(operationEvent);
        if (string.IsNullOrWhiteSpace(operationEvent.Message))
        {
            return;
        }

        string prefix = operationEvent.IsError ? "[ERROR] " : "[INFO] ";
        string line = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTime.Now:HH:mm:ss}{prefix}{operationEvent.Message}");
        try
        {
            lock (sync)
            {
                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
        }
        catch (IOException)
        {
            // Logging must not break a conversion operation.
        }
        catch (UnauthorizedAccessException)
        {
            // Logging must not break a conversion operation.
        }
    }
}
