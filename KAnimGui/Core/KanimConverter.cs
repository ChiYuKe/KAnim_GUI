// KanimConverter.cs
using KAnimGui.Models;
using KAnimGui.KAnimCore;
using KAnimGui.Application.Conversion;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KAnimGui.Core
{
    public class KanimConverter
    {
        public string PngPath { get; set; } = string.Empty;
        public string AnimPath { get; set; } = string.Empty;
        public string BuildPath { get; set; } = string.Empty;
        public string OutputDir { get; set; } = string.Empty;
        public bool StrictOrder { get; set; }
        public bool StrictMode { get; set; }
        public IProcessRunner? ProcessRunner { get; set; }
        public IKseExecutableLocator? ExecutableLocator { get; set; }
        public string? CliExecutablePath { get; set; }

        // 真实转换使用的完整输出目录（包含子文件夹）
        public string ActualOutputDir { get; private set; } = string.Empty;

        public async Task<ConversionResult> ConvertAsync(Action<string, bool> log, CancellationToken cancellationToken = default)
        {
            // 计算转换结果目录：在 OutputDir 下以 AnimPath 文件名前缀去除 "_anim" 创建子文件夹
            var animFileName = Path.GetFileNameWithoutExtension(AnimPath);
            var folderName = animFileName.EndsWith("_anim", StringComparison.OrdinalIgnoreCase)
                ? animFileName.Substring(0, animFileName.Length - "_anim".Length)
                : animFileName;

            ActualOutputDir = Path.Combine(OutputDir, folderName);
            Directory.CreateDirectory(ActualOutputDir);

            var ksePath = string.IsNullOrWhiteSpace(CliExecutablePath)
                ? ExecutableLocator?.FindExecutable()
                : CliExecutablePath;
            if (string.IsNullOrEmpty(ksePath))
            {
                return ConvertWithBuiltInExporter(log);
            }

            var cliResult = await RunCliAsync(ksePath, log, cancellationToken);
            if (string.Equals(cliResult.ErrorMessage, "转换已取消", StringComparison.Ordinal))
            {
                return cliResult;
            }

            if (cliResult.Success)
            {
                return cliResult;
            }

            log($"kanimal-cli.exe 转换失败（{cliResult.ErrorMessage ?? $"退出代码: {cliResult.ExitCode}"}），改用内置 KAnim 解码/SCML 导出内核。", true);
            return ConvertWithBuiltInExporter(log);
        }

        private async Task<ConversionResult> RunCliAsync(string executablePath, Action<string, bool> log, CancellationToken cancellationToken)
        {
            if (ProcessRunner == null)
            {
                return await CliProcessRunner.RunAsync(executablePath, BuildArguments(ActualOutputDir), log, cancellationToken);
            }

            var progress = new Progress<OperationEvent>(eventInfo => log(eventInfo.Message, eventInfo.IsError));
            var result = await ProcessRunner.RunAsync(executablePath, BuildArguments(ActualOutputDir), progress, cancellationToken);
            return new ConversionResult
            {
                Success = result.Succeeded,
                ExitCode = result.ExitCode,
                ErrorMessage = result.ErrorMessage
            };
        }

        private ConversionResult ConvertWithBuiltInExporter(Action<string, bool> log)
        {
            try
            {
                log("未找到 kanimal-cli.exe，改用内置 KAnim 解码/SCML 导出内核。", false);
                var scmlPath = KAnimToScmlExporter.Export(PngPath, BuildPath, AnimPath, ActualOutputDir);
                log($"内置内核导出完成: {scmlPath}", false);

                return new ConversionResult
                {
                    Success = true,
                    ExitCode = 0
                };
            }
            catch (Exception ex)
            {
                return new ConversionResult
                {
                    Success = false,
                    ExitCode = -1,
                    ErrorMessage = $"内置内核导出失败: {ex.Message}"
                };
            }
        }

        public string[] BuildArguments(string outputDir)
        {
            var args = new List<string> { "scml", PngPath };

            if (StrictOrder)
            {
                args.Add("-f");
                args.AddRange(new[] { BuildPath, AnimPath });
            }
            else
            {
                args.AddRange(new[] { AnimPath, BuildPath });
            }

            args.AddRange(new[] { "-o", outputDir });

            if (StrictMode)
                args.Add("-S");

            return args.ToArray();
        }
    }
}
