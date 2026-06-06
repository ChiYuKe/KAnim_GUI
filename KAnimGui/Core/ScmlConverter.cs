// ScmlConverter.cs
using KAnimGui.Models;
using KAnimGui.KAnimCore;
using KAnimGui.Utils;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KAnimGui.Core
{
    public class ScmlConverter
    {
        public string ScmlPath { get; set; } = string.Empty;
        public string OutputDir { get; set; } = string.Empty;
        public bool Interpolate { get; set; }
        public bool Debone { get; set; }

        // 真实转换使用的完整输出目录（包含子文件夹）
        public string ActualOutputDir { get; private set; } = string.Empty;

        public async Task<ConversionResult> ConvertAsync(Action<string, bool> log, CancellationToken cancellationToken = default)
        {
            var baseName = Path.GetFileNameWithoutExtension(ScmlPath);
            var folderName = baseName.EndsWith("_anim", StringComparison.OrdinalIgnoreCase)
                ? baseName.Substring(0, baseName.Length - "_anim".Length)
                : baseName;

            ActualOutputDir = Path.Combine(OutputDir, folderName);
            Directory.CreateDirectory(ActualOutputDir);

            var ksePath = KseLocator.FindExecutable();
            if (string.IsNullOrEmpty(ksePath))
            {
                return ConvertWithBuiltInExporter(log);
            }

            return await CliProcessRunner.RunAsync(ksePath, BuildArguments(ActualOutputDir), log, cancellationToken);
        }

        private ConversionResult ConvertWithBuiltInExporter(Action<string, bool> log)
        {
            if (Interpolate || Debone)
            {
                return new ConversionResult
                {
                    Success = false,
                    ExitCode = -1,
                    ErrorMessage = "内置 SCML -> KAnim 暂不支持插值或去骨骼；请取消这些选项，或配置 kanimal-cli.exe。"
                };
            }

            try
            {
                log("未找到 kanimal-cli.exe，改用内置 SCML 解码/KAnim 导出内核。", false);
                var result = ScmlToKanimExporter.Export(ScmlPath, ActualOutputDir);
                log($"内置内核导出完成: {result.PngPath}", false);
                log($"内置内核导出完成: {result.BuildPath}", false);
                log($"内置内核导出完成: {result.AnimPath}", false);

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
                    ErrorMessage = $"内置 SCML -> KAnim 导出失败: {ex.Message}"
                };
            }
        }

        public string[] BuildArguments(string outputDir)
        {
            var args = new List<string> { "kanim", ScmlPath, "-o", outputDir };

            if (Interpolate)
                args.Add("-i");

            if (Debone)
                args.Add("-b");

            return args.ToArray();
        }
    }
}
