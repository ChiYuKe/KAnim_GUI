// ScmlConverter.cs
using KAnimGui.Models;
using KAnimGui.Utils;
using System;
using System.IO;
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

        public async Task<ConversionResult> ConvertAsync(Action<string, bool> log)
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

            var baseName = Path.GetFileNameWithoutExtension(ScmlPath);
            var folderName = baseName.EndsWith("_anim", StringComparison.OrdinalIgnoreCase)
                ? baseName.Substring(0, baseName.Length - "_anim".Length)
                : baseName;

            ActualOutputDir = Path.Combine(OutputDir, folderName);
            Directory.CreateDirectory(ActualOutputDir);

            return await CliProcessRunner.RunAsync(ksePath, BuildArgs(ActualOutputDir), log);
        }

        private string[] BuildArgs(string outputDir)
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
