using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using MaterialDesignThemes.Wpf;
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
            ShowMessage(
                "请同时选择 .png、_anim.bytes 和 _build.bytes 文件",
                "缺少文件",
                PackIconKind.AlertCircle);
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
            ShowMessage("请先选择一个节点", "导出 PNG", PackIconKind.Information);
            return;
        }

        if (!exportService.TryResolveRegion(data, selected, out _, out var error))
        {
            ShowMessage(error, "导出 PNG", PackIconKind.AlertCircle);
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
            ShowMessage("导出成功！", "导出 PNG", PackIconKind.CheckCircle);
        }
        catch (Exception ex)
        {
            ShowMessage("导出失败：" + ex.Message, "导出 PNG", PackIconKind.AlertCircle);
        }
    }

    public void ExportTexture()
    {
        KAnimPackage? data = getData();
        if (data?.Texture is null)
        {
            ShowMessage("当前没有可用的贴图。", "导出贴图", PackIconKind.Information);
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
            ShowMessage("整张贴图导出成功！", "导出贴图", PackIconKind.CheckCircle);
        }
        catch (Exception ex)
        {
            ShowMessage("导出失败：" + ex.Message, "导出贴图", PackIconKind.AlertCircle);
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
            ShowMessage("当前没有可导出的动画。", "导出 GIF", PackIconKind.Information);
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

        string kanimName = GetKAnimName();
        string outputPath = Path.Combine(
            KAnimGifExportPathResolver.GetSingleExportDirectory(optionsWindow.OutputDirectory),
            KAnimGifExportPathResolver.BuildGifFileName(kanimName, bank.Name));

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
                outputPath,
                progress).ConfigureAwait(true);
            if (options.ShowCompletionNotification)
            {
                var messageBox = new CustomMessageBox(
                    $"GIF 导出成功！\n\n{outputPath}",
                    "导出 GIF",
                    PackIconKind.CheckCircle);
                if (owner is not null)
                {
                    messageBox.Owner = owner;
                }

                messageBox.ShowDialog();
            }
        }
        catch (Exception ex)
        {
            ShowMessage(
                $"GIF 导出失败：{ex.Message}",
                "导出 GIF",
                PackIconKind.AlertCircle);
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

    public async Task ExportAllAnimationsGifAsync()
    {
        if (isExportingGif)
        {
            return;
        }

        KAnimPackage? data = getData();
        IReadOnlyList<KAnimBank> banks = data?.Anim?.Banks
            .Where(candidate => candidate.Frames.Count > 0)
            .ToList() ?? [];
        if (banks.Count == 0)
        {
            ShowMessage("当前没有可导出的动画。", "批量导出 GIF", PackIconKind.Information);
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

        string kanimName = GetKAnimName();
        string outputDirectory = KAnimGifExportPathResolver.GetBatchExportDirectory(
            optionsWindow.OutputDirectory,
            kanimName);
        Directory.CreateDirectory(outputDirectory);

        isExportingGif = true;
        Window? owner = Window.GetWindow(treeView);
        string originalTitle = owner?.Title ?? string.Empty;
        Mouse.OverrideCursor = Cursors.Wait;
        stopPlayback();
        var failures = new List<string>();
        try
        {
            for (int index = 0; index < banks.Count; index++)
            {
                KAnimBank currentBank = banks[index];
                string outputPath = Path.Combine(
                    outputDirectory,
                    KAnimGifExportPathResolver.BuildGifFileName(kanimName, currentBank.Name));
                if (owner is not null)
                {
                    owner.Title = $"批量导出 GIF：{index + 1}/{banks.Count}";
                }

                try
                {
                    var progress = new Progress<int>(completed =>
                    {
                        if (owner is not null)
                        {
                            owner.Title = $"批量导出 GIF：{index + 1}/{banks.Count}（{completed}/{currentBank.Frames.Count}）";
                        }
                    });
                    await gifExportService.ExportAsync(
                        currentBank.Frames.Count,
                        currentBank.Rate,
                        frameIndex => renderService.RenderAnimationFrame(
                            currentBank,
                            frameIndex,
                            new PreviewRenderOptions(false, false, false, -1)),
                        options,
                        outputPath,
                        progress).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    failures.Add($"{currentBank.Name}: {ex.Message}");
                }
            }

            int succeeded = banks.Count - failures.Count;
            if (options.ShowCompletionNotification)
            {
                string detail = failures.Count == 0
                    ? $"已导出 {succeeded} 个动画。\n\n输出目录：{outputDirectory}"
                    : $"成功 {succeeded} 个，失败 {failures.Count} 个。\n\n输出目录：{outputDirectory}\n\n" +
                      string.Join("\n", failures);
                var messageBox = new CustomMessageBox(detail, "批量导出 GIF", PackIconKind.CheckCircle)
                {
                    Owner = owner
                };
                messageBox.ShowDialog();
            }
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
            ShowMessage("请先选择一个节点", "替换图片", PackIconKind.Information);
            return;
        }

        if (!exportService.TryResolveRegion(data, selected, out _, out var error))
        {
            ShowMessage(error, "替换图片", PackIconKind.AlertCircle);
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
            ShowMessage("替换成功！", "替换图片", PackIconKind.CheckCircle);
        }
        catch (Exception ex)
        {
            ShowMessage("替换失败：" + ex.Message, "替换图片", PackIconKind.AlertCircle);
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

    private string GetKAnimName()
    {
        string fileName = Path.GetFileNameWithoutExtension(fileController.Selection.AnimFile ?? string.Empty);
        if (fileName.EndsWith("_anim", StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName[..^5];
        }

        return SanitizeFileName(fileName);
    }

    public void Diagnose()
    {
        KAnimPackage? data = getData();
        if (data is null || !data.HasAnyData)
        {
            ShowMessage("请先打开一组 KAnim 文件。", "KAnim 诊断", PackIconKind.Information);
            return;
        }

        var diagnostics = KAnimDiagnostics.Analyze(data);
        ShowMessage(
            KAnimDiagnostics.FormatReport(data, diagnostics),
            "KAnim 诊断",
            PackIconKind.Information);
    }

    private void ShowMessage(string? message, string title, PackIconKind iconKind)
    {
        CustomMessageBox.Show(
            Window.GetWindow(treeView),
            message ?? "操作未完成。",
            title,
            iconKind);
    }
}
