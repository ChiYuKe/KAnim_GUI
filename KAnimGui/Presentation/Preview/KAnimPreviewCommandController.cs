using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using KanimLib;
using KAnimGui.KAnimCore;
using KAnimGui.Windows;

namespace KAnimGui.Presentation.Preview;

/// <summary>
/// Handles preview commands that require dialogs or package mutation while keeping
/// command policy out of the window code-behind.
/// </summary>
public sealed class KAnimPreviewCommandController
{
    private readonly TreeView treeView;
    private readonly DataGrid parameterGrid;
    private readonly AnimationViewport viewport;
    private readonly KAnimPreviewFileController fileController;
    private readonly KAnimPreviewExportService exportService;
    private readonly KAnimPreviewImageService imageService;
    private readonly KAnimPreviewRenderService renderService;
    private readonly KAnimPreviewGifExportService gifExportService;
    private readonly Func<KAnimPackage?> getData;
    private readonly Func<KAnimBank?> getCurrentBank;
    private readonly Func<object?> getSelectedObject;
    private readonly Action stopPlayback;
    private readonly Action cancelLoad;
    private readonly Action clearRenderData;
    private readonly Action<BitmapImage?, Rectangle[]?, PointF[]?> updateTexture;
    private readonly Action<KAnimPreviewFileSelection> openCompleteSelection;
    private bool isExportingGif;

    public bool HasAnyFile => fileController.HasAnyFile;

    public KAnimPreviewCommandController(
        TreeView treeView,
        DataGrid parameterGrid,
        AnimationViewport viewport,
        KAnimPreviewFileController fileController,
        KAnimPreviewExportService exportService,
        KAnimPreviewImageService imageService,
        KAnimPreviewRenderService renderService,
        KAnimPreviewGifExportService gifExportService,
        Func<KAnimPackage?> getData,
        Func<KAnimBank?> getCurrentBank,
        Func<object?> getSelectedObject,
        Action stopPlayback,
        Action cancelLoad,
        Action clearRenderData,
        Action<BitmapImage?, Rectangle[]?, PointF[]?> updateTexture,
        Action<KAnimPreviewFileSelection> openCompleteSelection)
    {
        this.treeView = treeView ?? throw new ArgumentNullException(nameof(treeView));
        this.parameterGrid = parameterGrid ?? throw new ArgumentNullException(nameof(parameterGrid));
        this.viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        this.fileController = fileController ?? throw new ArgumentNullException(nameof(fileController));
        this.exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        this.imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
        this.renderService = renderService ?? throw new ArgumentNullException(nameof(renderService));
        this.gifExportService = gifExportService ?? throw new ArgumentNullException(nameof(gifExportService));
        this.getData = getData ?? throw new ArgumentNullException(nameof(getData));
        this.getCurrentBank = getCurrentBank ?? throw new ArgumentNullException(nameof(getCurrentBank));
        this.getSelectedObject = getSelectedObject ?? throw new ArgumentNullException(nameof(getSelectedObject));
        this.stopPlayback = stopPlayback ?? throw new ArgumentNullException(nameof(stopPlayback));
        this.cancelLoad = cancelLoad ?? throw new ArgumentNullException(nameof(cancelLoad));
        this.clearRenderData = clearRenderData ?? throw new ArgumentNullException(nameof(clearRenderData));
        this.updateTexture = updateTexture ?? throw new ArgumentNullException(nameof(updateTexture));
        this.openCompleteSelection = openCompleteSelection ?? throw new ArgumentNullException(nameof(openCompleteSelection));
    }

    public void OpenFilesDialog()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = "KAnim files|*.png;*_anim.bytes;*_build.bytes|所有文件|*.*"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var selection = fileController.AddFiles(dialog.FileNames);
        if (selection.IsComplete)
        {
            openCompleteSelection(selection);
        }
        else
        {
            MessageBox.Show(
                "请同时选择 .png、_anim.bytes 和 _build.bytes 文件",
                "缺少文件",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    public void Reset()
    {
        stopPlayback();
        cancelLoad();
        fileController.Reset();
        clearRenderData();
        parameterGrid.ItemsSource = null;
        treeView.Items.Clear();
        viewport.ImageSource = null;
    }

    public void ExportSelectedImage()
    {
        KAnimPackage? data = getData();
        object? selected = getSelectedObject();
        if (data is null || selected is null)
        {
            MessageBox.Show("请先选择一个节点");
            return;
        }

        if (!exportService.TryResolveRegion(data, selected, out _, out var error))
        {
            MessageBox.Show(error);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG Image|*.png",
            FileName = "export.png"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            exportService.ExportRegion(data, selected, dialog.FileName);
            MessageBox.Show("导出成功！");
        }
        catch (Exception ex)
        {
            MessageBox.Show("导出失败：" + ex.Message);
        }
    }

    public void ExportTexture()
    {
        KAnimPackage? data = getData();
        if (data?.Texture is null)
        {
            MessageBox.Show("当前没有可用的贴图");
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG Image|*.png",
            FileName = "texture.png"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            imageService.SavePng(data.Texture, dialog.FileName);
            MessageBox.Show("整张贴图导出成功！");
        }
        catch (Exception ex)
        {
            MessageBox.Show("导出失败：" + ex.Message);
        }
    }

    public async Task ExportAnimationGifAsync()
    {
        if (isExportingGif)
        {
            return;
        }

        KAnimBank? bank = getCurrentBank();
        if (bank is null || bank.Frames.Count == 0)
        {
            MessageBox.Show("当前没有可导出的动画。", "导出 GIF", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var optionsWindow = new GifExportOptionsWindow(1.0, 768, 768)
        {
            Owner = Window.GetWindow(treeView)
        };
        if (optionsWindow.ShowDialog() != true || optionsWindow.Options is not { } options)
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "GIF 动画|*.gif",
            FileName = $"{SanitizeFileName(bank.Name)}.gif",
            DefaultExt = ".gif",
            AddExtension = true
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        isExportingGif = true;
        Window? owner = Window.GetWindow(treeView);
        string originalTitle = owner?.Title ?? string.Empty;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var progress = new Progress<int>(completed =>
            {
                if (owner is not null)
                {
                    owner.Title = $"导出 GIF：{completed}/{bank.Frames.Count}";
                }
            });
            stopPlayback();
            await gifExportService.ExportAsync(
                bank.Frames.Count,
                bank.Rate,
                index => renderService.RenderAnimationFrame(
                    bank,
                    index,
                    new PreviewRenderOptions(false, false, false, -1)),
                options,
                dialog.FileName,
                progress).ConfigureAwait(true);
            MessageBox.Show(
                $"GIF 导出成功！\n\n{dialog.FileName}",
                "导出 GIF",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"GIF 导出失败：{ex.Message}",
                "导出 GIF",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            if (owner is not null)
            {
                owner.Title = originalTitle;
            }

            isExportingGif = false;
        }
    }

    public void ReplaceSelectedImage()
    {
        KAnimPackage? data = getData();
        object? selected = getSelectedObject();
        if (data is null || selected is null)
        {
            MessageBox.Show("请先选择一个节点");
            return;
        }

        if (!exportService.TryResolveRegion(data, selected, out _, out var error))
        {
            MessageBox.Show(error);
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg",
            Title = "选择一张图片用于替换 Frame"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            data.Texture = exportService.ReplaceRegion(data, selected, dialog.FileName, out var region);
            updateTexture(data.Texture, new[] { region.Rectangle }, new[] { region.Pivot });
            MessageBox.Show("替换成功！");
        }
        catch (Exception ex)
        {
            MessageBox.Show("替换失败：" + ex.Message);
        }
    }

    private static string SanitizeFileName(string value)
    {
        string name = string.IsNullOrWhiteSpace(value) ? "animation" : value.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name;
    }

    public void Diagnose()
    {
        KAnimPackage? data = getData();
        if (data is null || !data.HasAnyData)
        {
            MessageBox.Show("请先打开一组 KAnim 文件。", "KAnim 诊断", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var diagnostics = KAnimDiagnostics.Analyze(data);
        MessageBox.Show(
            KAnimDiagnostics.FormatReport(data, diagnostics),
            "KAnim 诊断",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
