// KanimConverter.cs
using KAnimGui.Models;
using KAnimGui.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace KAnimGui.Core
{
    public class KanimConverter
    {
        public string PngPath { get; set; }
        public string AnimPath { get; set; }
        public string BuildPath { get; set; }
        public string OutputDir { get; set; }
        public bool StrictOrder { get; set; }
        public bool StrictMode { get; set; }

        // 真实转换使用的完整输出目录（包含子文件夹）
        public string ActualOutputDir { get; private set; }

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

            // 计算转换结果目录：在 OutputDir 下以 AnimPath 文件名前缀去除 "_anim" 创建子文件夹
            var animFileName = Path.GetFileNameWithoutExtension(AnimPath);
            var folderName = animFileName.EndsWith("_anim", StringComparison.OrdinalIgnoreCase)
                ? animFileName.Substring(0, animFileName.Length - "_anim".Length)
                : animFileName;

            var newOutputDir = Path.Combine(OutputDir, folderName);
            Directory.CreateDirectory(newOutputDir);
            ActualOutputDir = newOutputDir;

            // 临时替换 OutputDir 传入转换命令
            var oldOutputDir = OutputDir;
            OutputDir = newOutputDir;

            try
            {
                var args = BuildArgs();
                log($"执行命令: {ksePath} {args}", false);

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ksePath,
                        Arguments = args,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.OutputDataReceived += (s, e) => log(e.Data, false);
                process.ErrorDataReceived += (s, e) => log(e.Data, true);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.Run(() => process.WaitForExit());

                return new ConversionResult
                {
                    Success = process.ExitCode == 0,
                    ExitCode = process.ExitCode,
                    ErrorMessage = process.ExitCode == 0 ? null : $"退出代码: {process.ExitCode}"
                };
            }
            finally
            {
                OutputDir = oldOutputDir;
            }
        }

        private string BuildArgs()
        {
            var sb = new StringBuilder("scml");

            if (StrictOrder)
                sb.Append($" \"{PngPath}\" \"{BuildPath}\" \"{AnimPath}\"");
            else
                sb.Append($" \"{PngPath}\" \"{AnimPath}\" \"{BuildPath}\"");

            sb.Append($" -o \"{OutputDir}\"");

            if (StrictMode)
                sb.Append(" -S");

            return sb.ToString();
        }
    }
}
