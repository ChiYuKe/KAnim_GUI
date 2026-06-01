using KAnimGui.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace KAnimGui.Core
{
    internal static class CliProcessRunner
    {
        public static async Task<ConversionResult> RunAsync(
            string executablePath,
            IEnumerable<string> arguments,
            Action<string, bool> log)
        {
            var argumentList = arguments.ToArray();
            log($"执行命令: \"{executablePath}\" {string.Join(" ", argumentList.Select(QuoteArgument))}", false);

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

            foreach (var argument in argumentList)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.OutputDataReceived += (_, e) => LogData(e.Data, false, log);
            process.ErrorDataReceived += (_, e) => LogData(e.Data, true, log);

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                return new ConversionResult
                {
                    Success = process.ExitCode == 0,
                    ExitCode = process.ExitCode,
                    ErrorMessage = process.ExitCode == 0 ? null : $"退出代码: {process.ExitCode}"
                };
            }
            catch (Exception ex)
            {
                return new ConversionResult
                {
                    Success = false,
                    ExitCode = -1,
                    ErrorMessage = $"启动转换进程失败: {ex.Message}"
                };
            }
        }

        private static void LogData(string? data, bool isError, Action<string, bool> log)
        {
            if (!string.IsNullOrWhiteSpace(data))
            {
                log(data, isError);
            }
        }

        private static string QuoteArgument(string argument)
        {
            return argument.Any(char.IsWhiteSpace) || argument.Contains('"')
                ? $"\"{argument.Replace("\"", "\\\"")}\""
                : argument;
        }
    }
}
