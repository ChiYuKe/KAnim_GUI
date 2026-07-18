using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using KAnimGui.Core.Kanim;
using KAnimGui.Core.Preview;

namespace KAnimGui.Presentation.Preview;

/// <summary>
/// Composes the preview controllers and binds them to the named WPF controls.
/// </summary>
public sealed class KAnimPreviewWindowComposition : IDisposable
{
    private readonly FrameworkElement root;
    private readonly Window owner;
    private readonly DispatcherTimer playbackTimer = new(DispatcherPriority.Render);
    private readonly KAnimPreviewTreeController treeController;
    private readonly KAnimPreviewPlaybackCoordinator playbackCoordinator;
    private readonly KAnimPreviewFileController fileController;
    private readonly KAnimPreviewCommandController commandController;
    private KAnimPreviewSessionController sessionController = null!;

    public KAnimPreviewWindowComposition(
        Window owner,
        FrameworkElement root,
        KAnimPreviewLoadService loadService,
        KAnimPreviewRenderService renderService,
        KAnimPreviewImageService imageService,
        KAnimPreviewExportService exportService,
        KAnimPreviewGifExportService gifExportService)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.root = root ?? throw new ArgumentNullException(nameof(root));
        fileController = new KAnimPreviewFileController(
            Find<StackPanel>("ContentPanel"), Find<PackIcon>("UploadIcon"), Find<TextBlock>("HintText"));
        playbackCoordinator = new KAnimPreviewPlaybackCoordinator(
            playbackTimer,
            Find<ComboBox>("AnimationComboBox"),
            Find<Button>("PlayPauseButton"),
            Find<Button>("PreviousFrameButton"),
            Find<Button>("NextFrameButton"),
            Find<Slider>("FrameSlider"),
            Find<Slider>("PlaybackSpeedSlider"),
            Find<ListBox>("FrameListBox"),
            Find<ListBox>("ElementListBox"),
            Find<TextBlock>("FrameStatusText"),
            Find<TextBlock>("PlaybackSpeedText"),
            () => sessionController.Data,
            RenderCurrentAnimationFrame,
            UpdateParameterInfo,
            clearCaches: renderService.ClearCaches);
        treeController = new KAnimPreviewTreeController(
            Find<TreeView>("BuildTreeView"),
            new KAnimTreeModelBuilder(),
            () => sessionController.Data,
            () => playbackCoordinator.CurrentBank,
            bank => Find<ComboBox>("AnimationComboBox").SelectedItem = bank,
            (frameIndex, elementIndex) => playbackCoordinator.SelectFrameAndElement(frameIndex, elementIndex),
            StartPlayback,
            StopPlayback,
            RenderCurrentAnimationFrame,
            UpdateTextureView,
            UpdateParameterInfo);
        commandController = new KAnimPreviewCommandController(
            Find<TreeView>("BuildTreeView"),
            Find<DataGrid>("ParameterDataGrid"),
            Find<AnimationViewport>("PreviewViewport"),
            fileController,
            exportService,
            imageService,
            renderService,
            gifExportService,
            () => sessionController.Data,
            () => playbackCoordinator.CurrentBank,
            treeController.GetSelectedValue,
            StopPlayback,
            () => sessionController.CancelLoading(),
            () => sessionController.Clear(),
            UpdateTextureView,
            selection => OpenFiles(selection.TextureFile!, selection.BuildFile!, selection.AnimFile!));
        sessionController = new KAnimPreviewSessionController(
            loadService,
            renderService,
            fileController,
            new KAnimPreviewTexturePresenter(),
            Find<AnimationViewport>("PreviewViewport"),
            new PreviewParameterInspector(),
            Find<DataGrid>("ParameterDataGrid"),
            () => playbackCoordinator.CurrentBank,
            () => playbackCoordinator.CurrentFrameIndex,
            () => playbackCoordinator.SelectedElementIndex,
            () => new PreviewRenderOptions(
                Find<CheckBox>("ShowOriginCheckBox").IsChecked == true,
                Find<CheckBox>("ShowBoundsCheckBox").IsChecked == true,
                Find<CheckBox>("HighlightElementCheckBox").IsChecked == true,
                playbackCoordinator.SelectedElementIndex),
            () => treeController.Refresh(sessionController.Data),
            playbackCoordinator.Initialize,
            playbackCoordinator.RenderedFrame,
            value => Find<TextBlock>("FrameStatusText").Text = value,
            () => owner.Activate());
        var dropController = new KAnimPreviewDropController(
            Find<Card>("DropCard"),
            Find<PackIcon>("UploadIcon"),
            Find<TextBlock>("HintText"),
            (System.Windows.Media.Brush)owner.FindResource("PanelBackgroundBrush"),
            fileController,
            selection => OpenFiles(selection.TextureFile!, selection.BuildFile!, selection.AnimFile!),
            commandController.Reset,
            commandController.OpenFilesDialog);
        new KAnimPreviewWindowBinding(root).Attach(
            treeController,
            playbackCoordinator,
            commandController,
            dropController,
            () => SetPreviewBackground(true),
            () => SetPreviewBackground(false),
            owner.Close,
            RenderCurrentAnimationFrame);
    }

    public void OpenFiles(string textureFile, string buildFile, string animFile) =>
        sessionController.OpenFiles(textureFile, buildFile, animFile);

    public void OpenFilesAndPlay(string textureFile, string buildFile, string animFile) =>
        sessionController.OpenFilesAndPlay(textureFile, buildFile, animFile, StartPlayback);

    public Task OpenFilesAndPlayAsync(string textureFile, string buildFile, string animFile) =>
        sessionController.OpenFilesAndPlayAsync(textureFile, buildFile, animFile, StartPlayback);

    public void Dispose()
    {
        sessionController.Dispose();
        StopPlayback();
        playbackCoordinator.Dispose();
    }

    private T Find<T>(string name) where T : class => (root.FindName(name) as T)
        ?? throw new InvalidOperationException($"Preview control '{name}' was not found.");

    private void SetPreviewBackground(bool dark) =>
        Find<AnimationViewport>("PreviewViewport").SetBackground(dark);

    private void StopPlayback() => playbackCoordinator.Stop();

    private void StartPlayback() => playbackCoordinator.Start();

    private void RenderCurrentAnimationFrame() => sessionController.RenderCurrentAnimationFrame();

    private void UpdateParameterInfo(object selectedObj) => sessionController.UpdateParameterInfo(selectedObj);

    private void UpdateTextureView(BitmapImage? image, Rectangle[]? frames = null, PointF[]? pivots = null) =>
        sessionController.UpdateTextureView(image, frames, pivots);
}
