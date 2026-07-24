using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KAnimGui.Application.Platform;
using KAnimGui.Presentation;
using KAnimGui.Presentation.Theme;
using KAnimGui.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace KAnimGui;

public partial class MainWindow : Window
{
    private readonly MainWindowController controller;
    private readonly IServiceProvider serviceProvider;
    private KAnimRenderWindow? previewWorkspace;
    private OniResourceBridgeWorkspaceWindow? resourceBridgeWorkspace;

    public MainWindow(
        IServiceProvider serviceProvider,
        IFileSystemGateway fileSystem,
        IExternalLauncher externalLauncher)
    {
        InitializeComponent();
        this.serviceProvider = serviceProvider;
        controller = new MainWindowController(this, this, serviceProvider, fileSystem, externalLauncher);
        SettingsWorkspacePanel.Initialize(
            serviceProvider.GetRequiredService<IThemeService>(),
            controller.NotifySettingsChanged);
        Closed += MainWindow_Closed;
    }

    public void NotifySettingsChanged() => controller.NotifySettingsChanged();

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        SettingsWorkspacePanel.CommitPendingChanges();
        controller.Dispose();
    }

    private void Browse_Click(object sender, RoutedEventArgs e) => controller.Browse_Click(sender, e);
    private void BatchConvertButton_Click(object sender, RoutedEventArgs e) => controller.BatchConvertButton_Click(sender, e);
    private void BatchConvertScmlButton_Click(object sender, RoutedEventArgs e) => controller.BatchConvertScmlButton_Click(sender, e);
    private void ClearButton_Click(object sender, RoutedEventArgs e) => controller.ClearButton_Click(sender, e);
    private void ScmlClearButton_Click(object sender, RoutedEventArgs e) => controller.ScmlClearButton_Click(sender, e);
    private void HelpButton_Click(object sender, RoutedEventArgs e) => controller.HelpButton_Click(sender, e);
    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsWorkspacePanel.Visibility != Visibility.Visible)
        {
            SettingsWorkspacePanel.BeginEdit();
        }

        ShowWorkspace(WorkspaceKind.Settings);
    }
    private void GithubButton_Click(object sender, RoutedEventArgs e) => controller.GithubButton_Click(sender, e);
    private void PinButton_Click(object sender, RoutedEventArgs e) => controller.PinButton_Click(sender, e);

    private void ConversionNavButton_Click(object sender, RoutedEventArgs e)
    {
        if (LogWorkspace.Visibility == Visibility.Visible)
        {
            MainTabControl.SelectedIndex = LogTabControl.SelectedIndex;
        }

        ShowWorkspace(WorkspaceKind.Conversion);
    }

    private void PreviewNavButton_Click(object sender, RoutedEventArgs e)
    {
        KAnimRenderWindow preview = EnsurePreviewWorkspace();
        ShowWorkspace(WorkspaceKind.Preview);
        controller.LoadCurrentPreview(preview);
    }

    private void ResourceBridgeNavButton_Click(object sender, RoutedEventArgs e)
    {
        if (!controller.PrepareOniResourceBridge())
        {
            return;
        }

        EnsureResourceBridgeWorkspace();
        ShowWorkspace(WorkspaceKind.ResourceBridge);
    }

    private void LogNavButton_Click(object sender, RoutedEventArgs e)
    {
        LogTabControl.SelectedIndex = MainTabControl.SelectedIndex;
        ShowWorkspace(WorkspaceKind.Log);

        if (LogTabControl.SelectedIndex == 1)
        {
            ScmlLogTextBox.ScrollToEnd();
        }
        else
        {
            LogTextBox.ScrollToEnd();
        }
    }

    private void ClearActiveLogButton_Click(object sender, RoutedEventArgs e)
    {
        if (LogTabControl.SelectedIndex == 1)
        {
            controller.ClearScmlLog();
        }
        else
        {
            controller.ClearKanimLog();
        }
    }

    private KAnimRenderWindow EnsurePreviewWorkspace()
    {
        if (previewWorkspace is not null)
        {
            return previewWorkspace;
        }

        previewWorkspace = serviceProvider.GetRequiredService<KAnimRenderWindow>();
        previewWorkspace.CloseRequested += PreviewWorkspace_CloseRequested;
        PreviewWorkspaceHost.Content = previewWorkspace;
        return previewWorkspace;
    }

    private OniResourceBridgeWorkspaceWindow EnsureResourceBridgeWorkspace()
    {
        if (resourceBridgeWorkspace is not null)
        {
            return resourceBridgeWorkspace;
        }

        resourceBridgeWorkspace = serviceProvider.GetRequiredService<OniResourceBridgeWorkspaceWindow>();
        resourceBridgeWorkspace.PreviewRequestedAsync = OpenResourceBridgePreviewAsync;
        ResourceBridgeWorkspaceHost.Content = resourceBridgeWorkspace;
        return resourceBridgeWorkspace;
    }

    private async Task OpenResourceBridgePreviewAsync(string textureFile, string buildFile, string animFile)
    {
        KAnimRenderWindow preview = EnsurePreviewWorkspace();
        ShowWorkspace(WorkspaceKind.Preview);
        await preview.OpenFilesAndPlayAsync(textureFile, buildFile, animFile);
    }

    private void PreviewWorkspace_CloseRequested() => ShowWorkspace(WorkspaceKind.Conversion);

    private void ShowWorkspace(WorkspaceKind workspace)
    {
        if (SettingsWorkspacePanel.Visibility == Visibility.Visible &&
            workspace != WorkspaceKind.Settings)
        {
            SettingsWorkspacePanel.CommitPendingChanges();
        }

        ConversionWorkspace.Visibility =
            workspace == WorkspaceKind.Conversion ? Visibility.Visible : Visibility.Collapsed;
        PreviewWorkspaceHost.Visibility =
            workspace == WorkspaceKind.Preview ? Visibility.Visible : Visibility.Collapsed;
        ResourceBridgeWorkspaceHost.Visibility =
            workspace == WorkspaceKind.ResourceBridge ? Visibility.Visible : Visibility.Collapsed;
        LogWorkspace.Visibility =
            workspace == WorkspaceKind.Log ? Visibility.Visible : Visibility.Collapsed;
        SettingsWorkspacePanel.Visibility =
            workspace == WorkspaceKind.Settings ? Visibility.Visible : Visibility.Collapsed;

        SetNavigationSelected(ConversionNavButton, workspace == WorkspaceKind.Conversion);
        SetNavigationSelected(PreviewNavButton, workspace == WorkspaceKind.Preview);
        SetNavigationSelected(OniBridgeButton, workspace == WorkspaceKind.ResourceBridge);
        SetNavigationSelected(LogNavButton, workspace == WorkspaceKind.Log);
        SetNavigationSelected(SettingsNavButton, workspace == WorkspaceKind.Settings);
    }

    private static void SetNavigationSelected(Button button, bool isSelected)
    {
        if (isSelected)
        {
            button.SetResourceReference(Control.BackgroundProperty, "SelectedBrush");
            button.SetResourceReference(Control.ForegroundProperty, "AccentBrush");
            return;
        }

        button.ClearValue(Control.BackgroundProperty);
        button.ClearValue(Control.ForegroundProperty);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (WindowState == WindowState.Normal)
        {
            DragMove();
        }
    }

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeWindow_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowSurface is not null)
        {
            bool isMaximized = WindowState == WindowState.Maximized;
            double radius = isMaximized ? 0 : 12;
            Thickness frameMargin = isMaximized ? new Thickness(0) : new Thickness(14);

            WindowSurface.Margin = frameMargin;
            WindowShadow.Margin = frameMargin;
            WindowShadow.Visibility = isMaximized ? Visibility.Collapsed : Visibility.Visible;
            WindowSurface.CornerRadius = new CornerRadius(radius);
            TitleBarSurface.CornerRadius = new CornerRadius(radius, radius, 0, 0);
            FooterSurface.CornerRadius = new CornerRadius(0, 0, radius, radius);
        }
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private enum WorkspaceKind
    {
        Conversion,
        Preview,
        ResourceBridge,
        Log,
        Settings
    }
}
