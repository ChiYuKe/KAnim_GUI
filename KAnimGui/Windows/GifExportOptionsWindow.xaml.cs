using System.Globalization;
using System.Windows;

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
    }

    public KAnimGifExportOptions? Options { get; private set; }

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
        Properties.Default.Save();

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

    private static bool TryParseDouble(string? value, out double result)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result) ||
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
