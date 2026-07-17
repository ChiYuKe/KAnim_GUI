using System.Windows;
using KAnimGui.Application.Platform;
using KAnimGui.Presentation;

namespace KAnimGui;

public partial class MainWindow : Window
{
    private readonly MainWindowController controller;

    public MainWindow(
        IServiceProvider serviceProvider,
        IFileSystemGateway fileSystem,
        IExternalLauncher externalLauncher)
    {
        InitializeComponent();
        controller = new MainWindowController(this, this, serviceProvider, fileSystem, externalLauncher);
        Closed += MainWindow_Closed;
    }

    public void NotifySettingsChanged() => controller.NotifySettingsChanged();

    private void MainWindow_Closed(object? sender, EventArgs e) => controller.Dispose();

    private void Browse_Click(object sender, RoutedEventArgs e) => controller.Browse_Click(sender, e);
    private void BatchConvertButton_Click(object sender, RoutedEventArgs e) => controller.BatchConvertButton_Click(sender, e);
    private void BatchConvertScmlButton_Click(object sender, RoutedEventArgs e) => controller.BatchConvertScmlButton_Click(sender, e);
    private void ClearButton_Click(object sender, RoutedEventArgs e) => controller.ClearButton_Click(sender, e);
    private void ScmlClearButton_Click(object sender, RoutedEventArgs e) => controller.ScmlClearButton_Click(sender, e);
    private void HelpButton_Click(object sender, RoutedEventArgs e) => controller.HelpButton_Click(sender, e);
    private void SettingsButton_Click(object sender, RoutedEventArgs e) => controller.SettingsButton_Click(sender, e);
    private void GithubButton_Click(object sender, RoutedEventArgs e) => controller.GithubButton_Click(sender, e);
    private void PinButton_Click(object sender, RoutedEventArgs e) => controller.PinButton_Click(sender, e);
    private void TestButton_Click(object sender, RoutedEventArgs e) => controller.TestButton_Click(sender, e);
    private void OniBridgeButton_Click(object sender, RoutedEventArgs e) => controller.OniBridgeButton_Click(sender, e);
}
