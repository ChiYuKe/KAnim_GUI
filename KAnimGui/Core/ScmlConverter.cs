// ScmlConverter.cs
using KAnimGui.Models;
using KAnimGui.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace KAnimGui.Core
{
    public class ScmlConverter
    {
        public string ScmlPath { get; set; }
        public string OutputDir { get; set; }
        public bool Interpolate { get; set; }
        public bool Debone { get; set; }

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

            var baseName = Path.GetFileNameWithoutExtension(ScmlPath);
            var folderName = baseName.EndsWith("_anim", StringComparison.OrdinalIgnoreCase)
                ? baseName.Substring(0, baseName.Length - "_anim".Length)
                : baseName;

            var newOutputDir = Path.Combine(OutputDir, folderName);
            Directory.CreateDirectory(newOutputDir);
            ActualOutputDir = newOutputDir;

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
            var sb = new StringBuilder("kanim");

            sb.Append($" \"{ScmlPath}\" -o \"{OutputDir}\"");

            if (Interpolate)
                sb.Append(" -i");

            if (Debone)
                sb.Append(" -b");

            return sb.ToString();
        }
    }
}
