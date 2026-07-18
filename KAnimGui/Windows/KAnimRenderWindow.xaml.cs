using System.Threading.Tasks;
using System.Windows;
using KAnimGui.Presentation.Preview;

namespace KAnimGui.Windows;

public partial class KAnimRenderWindow : Window
{
    private readonly KAnimPreviewWindowComposition composition;

    public KAnimRenderWindow(
        KAnimPreviewLoadService previewLoadService,
        KAnimPreviewRenderService previewRenderService,
        KAnimPreviewImageService previewImageService,
        KAnimPreviewExportService previewExportService,
        KAnimPreviewGifExportService previewGifExportService)
    {
        InitializeComponent();
        composition = new KAnimPreviewWindowComposition(
            this,
            this,
            previewLoadService,
            previewRenderService,
            previewImageService,
            previewExportService,
            previewGifExportService);
        Closed += KAnimRenderWindow_Closed;
    }

    private void KAnimRenderWindow_Closed(object? sender, EventArgs e) => composition.Dispose();

    public void OpenFiles(string textureFile, string buildFile, string animFile) =>
        composition.OpenFiles(textureFile, buildFile, animFile);

    public void OpenFilesAndPlay(string textureFile, string buildFile, string animFile) =>
        composition.OpenFilesAndPlay(textureFile, buildFile, animFile);

    public Task OpenFilesAndPlayAsync(string textureFile, string buildFile, string animFile) =>
        composition.OpenFilesAndPlayAsync(textureFile, buildFile, animFile);
}
