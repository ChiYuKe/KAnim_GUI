using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KAnimGui.Presentation.Theme;

namespace KAnimGui.Windows;

public partial class SettingsWorkspace : UserControl
{
    private readonly DispatcherTimer autoSaveTimer;
    private IThemeService? themeService;
    private Action? settingsSaved;
    private bool hasPendingTextChange;
    private bool isLoaded;

    public SettingsWorkspace()
    {
        InitializeComponent();
        autoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        autoSaveTimer.Tick += AutoSaveTimer_Tick;
    }

    public void Initialize(
        IThemeService service,
        Action onSettingsSaved)
    {
        themeService = service ?? throw new ArgumentNullException(nameof(service));
        settingsSaved = onSettingsSaved ?? throw new ArgumentNullException(nameof(onSettingsSaved));
    }

    public void BeginEdit()
    {
        if (themeService is null)
        {
            throw new InvalidOperationException("设置工作区尚未初始化。");
        }

        isLoaded = false;
        ThemeComboBox.SelectedValue = themeService.CurrentTheme;
        EnableFeatureCheckBox.IsChecked = Properties.Default.OpenFolderAfterConvert;
        NoSuccessPopupCheckBox.IsChecked = Properties.Default.NoSuccessPopup;
        EnableTxtToBytesCheckBox.IsChecked = Properties.Default.EnableTxtToBytes;
        UseCustomKsePathCheckBox.IsChecked = Properties.Default.UseCustomKsePath;
        CustomKsePathTextBox.Text = Properties.Default.CustomKsePath;
        hasPendingTextChange = false;
        isLoaded = true;
    }

    public void CommitPendingChanges()
    {
        if (!hasPendingTextChange)
        {
            return;
        }

        autoSaveTimer.Stop();
        hasPendingTextChange = false;
        PersistSettings(notifyRuntime: true);
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!isLoaded ||
            themeService is null ||
            ThemeComboBox.SelectedValue is not AppTheme theme)
        {
            return;
        }

        themeService.Apply(theme);
        PersistSettings(notifyRuntime: false);
    }

    private void SettingCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isLoaded)
        {
            PersistSettings(notifyRuntime: true);
        }
    }

    private void CustomKsePathTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!isLoaded)
        {
            return;
        }

        hasPendingTextChange = true;
        autoSaveTimer.Stop();
        autoSaveTimer.Start();
    }

    private void AutoSaveTimer_Tick(object? sender, EventArgs e)
    {
        CommitPendingChanges();
    }

    private void PersistSettings(bool notifyRuntime)
    {
        if (!isLoaded || themeService is null)
        {
            return;
        }

        Properties.Default.Theme = themeService.CurrentTheme;
        Properties.Default.OpenFolderAfterConvert = EnableFeatureCheckBox.IsChecked == true;
        Properties.Default.NoSuccessPopup = NoSuccessPopupCheckBox.IsChecked == true;
        Properties.Default.EnableTxtToBytes = EnableTxtToBytesCheckBox.IsChecked == true;
        Properties.Default.UseCustomKsePath = UseCustomKsePathCheckBox.IsChecked == true;
        Properties.Default.CustomKsePath = CustomKsePathTextBox.Text;
        Properties.Default.Save();

        if (notifyRuntime)
        {
            settingsSaved?.Invoke();
        }
    }

    private void BrowseKseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*"
        };
        Window? owner = Window.GetWindow(this);
        if (owner is not null && dialog.ShowDialog(owner) == true)
        {
            CustomKsePathTextBox.Text = dialog.FileName;
        }
    }
}
