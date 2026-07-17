using KAnimGui.Application.ResourceBridge;

namespace KAnimGui.Presentation.ResourceBridge;

public sealed class MainWindowWorkspaceGateway : IKanimWorkspaceGateway
{
    private readonly MainWindow mainWindow;

    public MainWindowWorkspaceGateway(MainWindow mainWindow)
    {
        this.mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
    }

    public Task OpenAsync(ExportArtifact artifact, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (artifact.PngPath == null || artifact.AnimPath == null || artifact.BuildPath == null)
        {
            throw new InvalidOperationException("导入主工作台需要完整的 KAnim 文件组。");
        }

        mainWindow.ImportKanimFileSet(artifact.PngPath, artifact.AnimPath, artifact.BuildPath);
        return Task.CompletedTask;
    }
}
