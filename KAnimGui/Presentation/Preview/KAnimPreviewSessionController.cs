using System.Drawing;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using KanimLib;
using KAnimGui.Core.Preview;

namespace KAnimGui.Presentation.Preview;

/// <summary>
/// Owns the loaded preview package, asynchronous loading session, and current-frame rendering.
/// </summary>
public sealed class KAnimPreviewSessionController : IDisposable
{
    private readonly KAnimPreviewLoadService loadService;
    private readonly KAnimPreviewRenderService renderService;
    private readonly KAnimPreviewFileController fileController;
    private readonly KAnimPreviewTexturePresenter texturePresenter;
    private readonly AnimationViewport viewport;
    private readonly PreviewParameterInspector parameterInspector;
    private readonly DataGrid parameterGrid;
    private readonly Func<KAnimBank?> getCurrentBank;
    private readonly Func<int> getCurrentFrameIndex;
    private readonly Func<int> getSelectedElementIndex;
    private readonly Func<PreviewRenderOptions> getRenderOptions;
    private readonly Action refreshTree;
    private readonly Action initializePlayback;
    private readonly Action<KAnimFrame> renderedFrame;
    private readonly Action<string> setStatus;
    private readonly Action activateWindow;
    private CancellationTokenSource? loadCancellation;

    public KAnimPreviewSessionController(
        KAnimPreviewLoadService loadService,
        KAnimPreviewRenderService renderService,
        KAnimPreviewFileController fileController,
        KAnimPreviewTexturePresenter texturePresenter,
        AnimationViewport viewport,
        PreviewParameterInspector parameterInspector,
        DataGrid parameterGrid,
        Func<KAnimBank?> getCurrentBank,
        Func<int> getCurrentFrameIndex,
        Func<int> getSelectedElementIndex,
        Func<PreviewRenderOptions> getRenderOptions,
        Action refreshTree,
        Action initializePlayback,
        Action<KAnimFrame> renderedFrame,
        Action<string> setStatus,
        Action activateWindow)
    {
        this.loadService = loadService ?? throw new ArgumentNullException(nameof(loadService));
        this.renderService = renderService ?? throw new ArgumentNullException(nameof(renderService));
        this.fileController = fileController ?? throw new ArgumentNullException(nameof(fileController));
        this.texturePresenter = texturePresenter ?? throw new ArgumentNullException(nameof(texturePresenter));
        this.viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        this.parameterInspector = parameterInspector ?? throw new ArgumentNullException(nameof(parameterInspector));
        this.parameterGrid = parameterGrid ?? throw new ArgumentNullException(nameof(parameterGrid));
        this.getCurrentBank = getCurrentBank ?? throw new ArgumentNullException(nameof(getCurrentBank));
        this.getCurrentFrameIndex = getCurrentFrameIndex ?? throw new ArgumentNullException(nameof(getCurrentFrameIndex));
        this.getSelectedElementIndex = getSelectedElementIndex ?? throw new ArgumentNullException(nameof(getSelectedElementIndex));
        this.getRenderOptions = getRenderOptions ?? throw new ArgumentNullException(nameof(getRenderOptions));
        this.refreshTree = refreshTree ?? throw new ArgumentNullException(nameof(refreshTree));
        this.initializePlayback = initializePlayback ?? throw new ArgumentNullException(nameof(initializePlayback));
        this.renderedFrame = renderedFrame ?? throw new ArgumentNullException(nameof(renderedFrame));
        this.setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
        this.activateWindow = activateWindow ?? throw new ArgumentNullException(nameof(activateWindow));
    }

    public KAnimPackage? Data { get; private set; }

    public void OpenFiles(string textureFile, string buildFile, string animFile)
    {
        fileController.SetFiles(new[] { textureFile, buildFile, animFile });
        OpenData(loadService.Load(textureFile, buildFile, animFile));
    }

    public void OpenFilesAndPlay(string textureFile, string buildFile, string animFile, Action startPlayback)
    {
        ArgumentNullException.ThrowIfNull(startPlayback);
        OpenFiles(textureFile, buildFile, animFile);
        startPlayback();
        activateWindow();
    }

    public async Task OpenFilesAndPlayAsync(
        string textureFile,
        string buildFile,
        string animFile,
        Action startPlayback)
    {
        ArgumentNullException.ThrowIfNull(startPlayback);
        CancelLoading();
        loadCancellation?.Dispose();
        loadCancellation = new CancellationTokenSource();
        CancellationTokenSource requestCancellation = loadCancellation;

        try
        {
            var selection = fileController.SetFiles(new[] { textureFile, buildFile, animFile });
            if (!selection.IsComplete)
            {
                return;
            }

            setStatus("正在加载预览...");
            KAnimPackage package = await loadService.LoadAsync(
                selection.TextureFile!,
                selection.BuildFile!,
                selection.AnimFile!,
                requestCancellation.Token);
            requestCancellation.Token.ThrowIfCancellationRequested();
            OpenData(package);
            startPlayback();
            activateWindow();
        }
        catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested)
        {
            // A newer preview request or window close superseded this load.
        }
        finally
        {
            if (ReferenceEquals(loadCancellation, requestCancellation))
            {
                loadCancellation = null;
            }

            requestCancellation.Dispose();
        }
    }

    public void RenderCurrentAnimationFrame()
    {
        if (Data?.Texture is null ||
            Data.Build is null ||
            getCurrentBank() is not { } bank ||
            bank.Frames.Count == 0)
        {
            return;
        }

        int frameIndex = getCurrentFrameIndex();
        if (frameIndex < 0 || frameIndex >= bank.Frames.Count)
        {
            return;
        }

        KAnimFrame frame = bank.Frames[frameIndex];
        viewport.ImageSource = renderService.RenderAnimationFrame(
            bank,
            frameIndex,
            getRenderOptions());
        renderedFrame(frame);
    }

    public void UpdateParameterInfo(object selectedObject)
    {
        parameterGrid.ItemsSource = parameterInspector.Describe(
            selectedObject,
            Data?.Texture?.PixelWidth,
            Data?.Texture?.PixelHeight);
    }

    public void UpdateTextureView(
        BitmapImage? image,
        Rectangle[]? frames = null,
        PointF[]? pivots = null)
    {
        texturePresenter.Show(viewport, image, Data?.Build, frames, pivots);
    }

    public void Clear()
    {
        CancelLoading();
        Data = null;
        renderService.SetData(null);
        parameterGrid.ItemsSource = null;
        viewport.ImageSource = null;
    }

    public void CancelLoading() => loadCancellation?.Cancel();

    public void Dispose()
    {
        CancelLoading();
        loadCancellation?.Dispose();
        loadCancellation = null;
        renderService.SetData(null);
    }

    private void OpenData(KAnimPackage package)
    {
        Data = package;
        renderService.SetData(package);
        UpdateTextureView(package.Texture);
        refreshTree();
        initializePlayback();
    }
}
