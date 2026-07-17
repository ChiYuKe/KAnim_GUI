using System.Windows;
using System.Windows.Controls;
using KAnimGui.Application.Platform;
using KAnimGui.Presentation.ResourceBridge;

namespace KAnimGui.Windows;

public partial class OniResourceBridgeWorkspaceWindow : Window
{
    private readonly OniResourceBridgeViewModel viewModel;
    private readonly IFileSystemGateway fileSystem;
    private readonly IExternalLauncher externalLauncher;

    public OniResourceBridgeWorkspaceWindow(
        OniResourceBridgeViewModel viewModel,
        IFileSystemGateway fileSystem,
        IExternalLauncher externalLauncher)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.externalLauncher = externalLauncher ?? throw new ArgumentNullException(nameof(externalLauncher));
        InitializeComponent();
        DataContext = viewModel;
        Loaded += Window_Loaded;
        Closed += Window_Closed;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await viewModel.InitializeAsync();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        viewModel.Dispose();
    }

    private void ResourceListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        viewModel.SetSelectedResources(ResourceListView.SelectedItems.Cast<BridgeResourceRowViewModel>());
    }

    private void OpenExportFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            fileSystem.EnsureDirectory(viewModel.ExportDirectory);
            externalLauncher.Open(viewModel.ExportDirectory);
        }
        catch (Exception ex)
        {
            viewModel.StatusText = $"打开目录失败：{ex.Message}";
        }
    }

}
