using System.Windows;
using System.Windows.Controls;
using KAnimGui.Application.Platform;
using KAnimGui.Application.ResourceBridge;
using KAnimGui.Presentation.ResourceBridge;
using Microsoft.Extensions.DependencyInjection;

namespace KAnimGui.Windows;

public partial class OniResourceBridgeWorkspaceWindow : Window
{
    private readonly OniResourceBridgeViewModel viewModel;
    private readonly IFileSystemGateway fileSystem;
    private readonly IExternalLauncher externalLauncher;
    private readonly IServiceProvider services;
    private KAnimRenderWindow? previewWindow;

    public OniResourceBridgeWorkspaceWindow(
        OniResourceBridgeViewModel viewModel,
        IFileSystemGateway fileSystem,
        IExternalLauncher externalLauncher,
        IServiceProvider services)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.externalLauncher = externalLauncher ?? throw new ArgumentNullException(nameof(externalLauncher));
        this.services = services ?? throw new ArgumentNullException(nameof(services));
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

    private async void ResourceRowAction_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.IsBusy ||
            (sender as FrameworkElement)?.DataContext is not BridgeResourceRowViewModel row ||
            !row.CanExport)
        {
            return;
        }

        if (!row.CanPreview)
        {
            await viewModel.ExportResourceCommand.ExecuteAsync(row);
            return;
        }

        try
        {
            ExportArtifact artifact = await viewModel.PrepareAnimationPreviewAsync(row);
            if (artifact.PngPath is null || artifact.AnimPath is null || artifact.BuildPath is null)
            {
                throw new InvalidOperationException("预览文件不完整，缺少 PNG、anim 或 build 文件。");
            }

            if (previewWindow is null)
            {
                previewWindow = services.GetRequiredService<KAnimRenderWindow>();
                previewWindow.Owner = this;
                previewWindow.Closed += PreviewWindow_Closed;
                previewWindow.Show();
            }
            else
            {
                previewWindow.Activate();
            }

            await previewWindow.OpenFilesAndPlayAsync(
                artifact.PngPath,
                artifact.BuildPath,
                artifact.AnimPath);
        }
        catch (Exception ex)
        {
            viewModel.StatusText = $"预览失败：{ex.Message}";
        }
    }

    private void PreviewWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is KAnimRenderWindow window)
        {
            window.Closed -= PreviewWindow_Closed;
        }

        previewWindow = null;
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
