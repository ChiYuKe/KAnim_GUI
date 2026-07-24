using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KAnimGui.Presentation.Preview;

namespace KAnimGui.Windows;

public partial class KAnimRenderWindow : UserControl, IDisposable
{
    private readonly KAnimPreviewLoadService previewLoadService;
    private readonly KAnimPreviewRenderService previewRenderService;
    private readonly KAnimPreviewImageService previewImageService;
    private readonly KAnimPreviewExportService previewExportService;
    private readonly KAnimPreviewGifExportService previewGifExportService;
    private KAnimPreviewWindowComposition? composition;
    private bool disposed;

    public KAnimRenderWindow(
        KAnimPreviewLoadService previewLoadService,
        KAnimPreviewRenderService previewRenderService,
        KAnimPreviewImageService previewImageService,
        KAnimPreviewExportService previewExportService,
        KAnimPreviewGifExportService previewGifExportService)
    {
        this.previewLoadService = previewLoadService;
        this.previewRenderService = previewRenderService;
        this.previewImageService = previewImageService;
        this.previewExportService = previewExportService;
        this.previewGifExportService = previewGifExportService;
        InitializeComponent();
        Loaded += KAnimRenderWorkspace_Loaded;
    }

    public event Action? CloseRequested;

    public void OpenFiles(string textureFile, string buildFile, string animFile) =>
        EnsureComposition().OpenFiles(textureFile, buildFile, animFile);

    public void OpenFilesAndPlay(string textureFile, string buildFile, string animFile) =>
        EnsureComposition().OpenFilesAndPlay(textureFile, buildFile, animFile);

    public Task OpenFilesAndPlayAsync(string textureFile, string buildFile, string animFile) =>
        EnsureComposition().OpenFilesAndPlayAsync(textureFile, buildFile, animFile);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Loaded -= KAnimRenderWorkspace_Loaded;
        composition?.Dispose();
        composition = null;
    }

    private void KAnimRenderWorkspace_Loaded(object sender, RoutedEventArgs e) => EnsureComposition();

    private KAnimPreviewWindowComposition EnsureComposition()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (composition is not null)
        {
            return composition;
        }

        Window owner = Window.GetWindow(this) ?? System.Windows.Application.Current.MainWindow
            ?? throw new InvalidOperationException("预览工作区尚未连接到主窗口。");
        composition = new KAnimPreviewWindowComposition(
            owner,
            this,
            previewLoadService,
            previewRenderService,
            previewImageService,
            previewExportService,
            previewGifExportService,
            () => CloseRequested?.Invoke());
        return composition;
    }
}
