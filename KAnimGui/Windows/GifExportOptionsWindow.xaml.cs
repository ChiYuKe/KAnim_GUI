using System.Globalization;
using System.IO;
using System.Windows;
using Ookii.Dialogs.Wpf;
using KAnimGui.Presentation.Preview;

namespace KAnimGui.Windows;

public partial class GifExportOptionsWindow : Window
{
    public GifExportOptionsWindow(double defaultPlaybackSpeed, int defaultWidth, int defaultHeight)
    {
        InitializeComponent();
        double savedPlaybackSpeed = Properties.Default.GifExportPlaybackSpeed;
        if (double.IsNaN(savedPlaybackSpeed) ||
            double.IsInfinity(savedPlaybackSpeed) ||
            savedPlaybackSpeed is < 0.1 or > 2.0)
        {
            savedPlaybackSpeed = defaultPlaybackSpeed;
        }

        int savedWidth = Properties.Default.GifExportWidth is >= 16 and <= 4096
            ? Properties.Default.GifExportWidth
            : defaultWidth;
        int savedHeight = Properties.Default.GifExportHeight is >= 16 and <= 4096
            ? Properties.Default.GifExportHeight
            : defaultHeight;

        PlaybackSpeedTextBox.Text = savedPlaybackSpeed.ToString("0.##", CultureInfo.CurrentCulture);
        WidthTextBox.Text = savedWidth.ToString(CultureInfo.InvariantCulture);
        HeightTextBox.Text = savedHeight.ToString(CultureInfo.InvariantCulture);
        ScalingModeComboBox.SelectedIndex = Math.Clamp(Properties.Default.GifExportScalingMode, 0, 3);
        ShowCompletionNotificationCheckBox.IsChecked =
            Properties.Default.ShowGifExportCompletionNotification;
        OutputDirectoryTextBox.Text = KAnimGifExportPathResolver.GetConfiguredDirectory();
    }

    public KAnimGifExportOptions? Options { get; private set; }
    public string OutputDirectory { get; private set; } = string.Empty;

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseDouble(PlaybackSpeedTextBox.Text, out double playbackSpeed) ||
            playbackSpeed is < 0.1 or > 2.0)
        {
            ShowError("播放速度必须是 0.1 到 2.0 倍之间的数字。");
            return;
        }

        if (!int.TryParse(WidthTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int width) ||
            width is < 16 or > 4096)
        {
            ShowError("宽度必须是 16 到 4096 之间的整数。");
            return;
        }

        if (!int.TryParse(HeightTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int height) ||
            height is < 16 or > 4096)
        {
            ShowError("高度必须是 16 到 4096 之间的整数。");
            return;
        }

        string outputDirectory = OutputDirectoryTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            ShowError("请选择 GIF 输出目录。");
            return;
        }

        try
        {
            outputDirectory = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(outputDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException or PathTooLongException)
        {
            ShowError($"输出目录不可用：{ex.Message}");
            return;
        }

        bool showCompletionNotification = ShowCompletionNotificationCheckBox.IsChecked == true;
        KAnimGifScalingMode scalingMode = ScalingModeComboBox.SelectedIndex switch
        {
            1 => KAnimGifScalingMode.Bicubic,
            2 => KAnimGifScalingMode.Spline,
            3 => KAnimGifScalingMode.Nearest,
            _ => KAnimGifScalingMode.Lanczos
        };
        Properties.Default.ShowGifExportCompletionNotification = showCompletionNotification;
        Properties.Default.GifExportPlaybackSpeed = playbackSpeed;
        Properties.Default.GifExportWidth = width;
        Properties.Default.GifExportHeight = height;
        Properties.Default.GifExportScalingMode = (int)scalingMode;
        Properties.Default.GifExportOutputDirectory = outputDirectory;
        Properties.Default.Save();
        OutputDirectory = outputDirectory;

        Options = new KAnimGifExportOptions(
            playbackSpeed,
            width,
            height,
            showCompletionNotification,
            scalingMode);
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
    }

    private void BrowseOutputDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new VistaFolderBrowserDialog
        {
            SelectedPath = OutputDirectoryTextBox.Text.Trim(),
            Description = "选择 GIF 输出目录"
        };
        if (dialog.ShowDialog() == true)
        {
            OutputDirectoryTextBox.Text = dialog.SelectedPath;
        }
    }

    private static bool TryParseDouble(string? value, out double result)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result) ||
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
