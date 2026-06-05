// KanimConverter.cs
using KAnimGui.Models;
using KAnimGui.Utils;
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

        // 真实转换使用的完整输出目录（包含子文件夹）
        public string ActualOutputDir { get; private set; } = string.Empty;

        public async Task<ConversionResult> ConvertAsync(Action<string, bool> log, CancellationToken cancellationToken = default)
        {
            var ksePath = KseLocator.FindExecutable();
            if (string.IsNullOrEmpty(ksePath))
            {
                return new ConversionResult
                {
                    Success = false,
                    ErrorMessage = "找不到 kanimal-cli.exe"
                };
            }

            // 计算转换结果目录：在 OutputDir 下以 AnimPath 文件名前缀去除 "_anim" 创建子文件夹
            var animFileName = Path.GetFileNameWithoutExtension(AnimPath);
            var folderName = animFileName.EndsWith("_anim", StringComparison.OrdinalIgnoreCase)
                ? animFileName.Substring(0, animFileName.Length - "_anim".Length)
                : animFileName;

            ActualOutputDir = Path.Combine(OutputDir, folderName);
            Directory.CreateDirectory(ActualOutputDir);

            return await CliProcessRunner.RunAsync(ksePath, BuildArguments(ActualOutputDir), log, cancellationToken);
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
