using System;
using System.Windows.Controls;
using System.Windows.Threading;

namespace KAnimGui.Utils
{
    public class LogManager
    {
        private readonly TextBox logBox;
        private readonly TextBlock statusText;

        public LogManager(TextBox logBox, TextBlock statusText)
        {
            this.logBox = logBox;
            this.statusText = statusText;
        }

        public void Log(string message, bool isError = false)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            logBox.Dispatcher.Invoke(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                var prefix = isError ? "[ERROR] " : "[INFO] ";
                var text = $"{timestamp}{prefix}{message}";

                logBox.AppendText(text + Environment.NewLine);
                logBox.ScrollToEnd();

                statusText.Text = message.Length > 50 ? message.Substring(0, 50) + "..." : message;
            });
        }
    }
}
