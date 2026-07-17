using System.Diagnostics;
using KAnimGui.Application.Conversion;

namespace KAnimGui.Infrastructure.Conversion;

public sealed class CliProcessRunnerService : IProcessRunner
{
    public async Task<ProcessExecutionResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        IProgress<OperationEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        string operationId = Guid.NewGuid().ToString("N");
        progress?.Report(new OperationEvent(
            operationId,
            $"执行命令: \"{executablePath}\" {string.Join(" ", arguments.Select(QuoteArgument))}",
            Stage: "process"));

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.OutputDataReceived += (_, args) => ReportLine(progress, operationId, args.Data, false);
        process.ErrorDataReceived += (_, args) => ReportLine(progress, operationId, args.Data, true);

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return new ProcessExecutionResult(
                process.ExitCode == 0,
                process.ExitCode,
                process.ExitCode == 0 ? null : $"退出代码: {process.ExitCode}",
                false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            progress?.Report(new OperationEvent(operationId, "转换进程已取消", true, Stage: "process"));
            return new ProcessExecutionResult(false, -1, "转换已取消", true);
        }
        catch (Exception ex)
        {
            progress?.Report(new OperationEvent(operationId, $"启动转换进程失败: {ex.Message}", true, Stage: "process", Exception: ex));
            return new ProcessExecutionResult(false, -1, $"启动转换进程失败: {ex.Message}", false);
        }
    }

    private static void ReportLine(
        IProgress<OperationEvent>? progress,
        string operationId,
        string? line,
        bool isError)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            progress?.Report(new OperationEvent(operationId, line, isError, Stage: "process"));
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Cancellation must retain the original cancellation result.
        }
    }

    private static string QuoteArgument(string argument)
    {
        return argument.Any(char.IsWhiteSpace) || argument.Contains('"')
            ? $"\"{argument.Replace("\"", "\\\"")}\""
            : argument;
    }
}
