using System;
using System.IO;
using System.Windows.Controls;

namespace KAnimGui.Utils
{
    public class LogManager
    {
        private readonly TextBox logBox;
        private readonly TextBlock statusText;
        private readonly object fileLock = new();

        public string LogFilePath { get; }

        public LogManager(TextBox logBox, TextBlock statusText, string logName)
        {
            this.logBox = logBox;
            this.statusText = statusText;

            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "KSE_Output",
                "Logs");

            Directory.CreateDirectory(logDir);
            LogFilePath = Path.Combine(logDir, $"{logName}_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        }

        public void Log(string message, bool isError = false)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            var text = FormatMessage(message, isError);
            WriteFileLog(text);

            logBox.Dispatcher.Invoke(() =>
            {
                logBox.AppendText(text + Environment.NewLine);
                logBox.ScrollToEnd();

                statusText.Text = message.Length > 50 ? message.Substring(0, 50) + "..." : message;
            });
        }

        private static string FormatMessage(string message, bool isError)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var prefix = isError ? "[ERROR] " : "[INFO] ";
            return $"{timestamp}{prefix}{message}";
        }

        private void WriteFileLog(string text)
        {
            try
            {
                lock (fileLock)
                {
                    File.AppendAllText(LogFilePath, text + Environment.NewLine);
                }
            }
            catch
            {
                // 日志写盘失败不应影响转换流程。
            }
        }
    }
}
