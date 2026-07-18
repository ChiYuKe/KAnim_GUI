using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KAnimGui.Presentation.Preview;

namespace KAnimGui.Windows;

public partial class KAnimPreviewSettingsWindow : Window
{
    private static readonly PreviewShortcut DefaultResetShortcut = new(Key.H, ModifierKeys.None);
    private static readonly PreviewShortcut DefaultPlayPauseShortcut = new(Key.Space, ModifierKeys.None);

    public KAnimPreviewSettingsWindow()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        ResetViewShortcutTextBox.Text = PreviewShortcut.Parse(
            Properties.Default.PreviewResetViewShortcut,
            DefaultResetShortcut).ToString();
        PlayPauseShortcutTextBox.Text = PreviewShortcut.Parse(
            Properties.Default.PreviewPlayPauseShortcut,
            DefaultPlayPauseShortcut).ToString();
        WheelAnimationSwitchCheckBox.IsChecked = Properties.Default.PreviewWheelAnimationSwitch;
        AutoPlayAnimationCheckBox.IsChecked = Properties.Default.PreviewAutoPlayAnimation;
        ShowOriginCheckBox.IsChecked = Properties.Default.PreviewShowOrigin;
        ShowBoundsCheckBox.IsChecked = Properties.Default.PreviewShowBounds;
        HighlightElementCheckBox.IsChecked = Properties.Default.PreviewHighlightElement;
        DarkBackgroundCheckBox.IsChecked = Properties.Default.PreviewDarkBackground;
    }

    private void ShortcutTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        Key key = e.Key switch
        {
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.System => e.SystemKey,
            _ => e.Key
        };
        if (key is Key.Delete or Key.Back)
        {
            textBox.Text = "None";
        }
        else if (!PreviewShortcut.IsModifierKey(key))
        {
            textBox.Text = PreviewShortcut.FromKeyEvent(e).ToString();
        }

        textBox.CaretIndex = textBox.Text.Length;
        e.Handled = true;
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        ResetViewShortcutTextBox.Text = DefaultResetShortcut.ToString();
        PlayPauseShortcutTextBox.Text = DefaultPlayPauseShortcut.ToString();
        WheelAnimationSwitchCheckBox.IsChecked = true;
        AutoPlayAnimationCheckBox.IsChecked = true;
        ShowOriginCheckBox.IsChecked = true;
        ShowBoundsCheckBox.IsChecked = false;
        HighlightElementCheckBox.IsChecked = true;
        DarkBackgroundCheckBox.IsChecked = false;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Properties.Default.PreviewResetViewShortcut = PreviewShortcut.Parse(
            ResetViewShortcutTextBox.Text,
            DefaultResetShortcut).ToString();
        Properties.Default.PreviewPlayPauseShortcut = PreviewShortcut.Parse(
            PlayPauseShortcutTextBox.Text,
            DefaultPlayPauseShortcut).ToString();
        Properties.Default.PreviewWheelAnimationSwitch = WheelAnimationSwitchCheckBox.IsChecked == true;
        Properties.Default.PreviewAutoPlayAnimation = AutoPlayAnimationCheckBox.IsChecked == true;
        Properties.Default.PreviewShowOrigin = ShowOriginCheckBox.IsChecked == true;
        Properties.Default.PreviewShowBounds = ShowBoundsCheckBox.IsChecked == true;
        Properties.Default.PreviewHighlightElement = HighlightElementCheckBox.IsChecked == true;
        Properties.Default.PreviewDarkBackground = DarkBackgroundCheckBox.IsChecked == true;
        Properties.Default.Save();
        DialogResult = true;
    }
}
