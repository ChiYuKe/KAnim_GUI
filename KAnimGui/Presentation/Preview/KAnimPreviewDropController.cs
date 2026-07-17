using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace KAnimGui.Presentation.Preview;

/// <summary>
/// Handles drag/drop feedback and delegates package selection to the file controller.
/// </summary>
public sealed class KAnimPreviewDropController
{
    private readonly Card card;
    private readonly PackIcon uploadIcon;
    private readonly System.Windows.Controls.TextBlock hintText;
    private readonly Brush defaultBackground;
    private readonly KAnimPreviewFileController fileController;
    private readonly Action<KAnimPreviewFileSelection> openCompleteSelection;
    private readonly Action reset;
    private readonly Action openDialog;

    public KAnimPreviewDropController(
        Card card,
        PackIcon uploadIcon,
        System.Windows.Controls.TextBlock hintText,
        Brush defaultBackground,
        KAnimPreviewFileController fileController,
        Action<KAnimPreviewFileSelection> openCompleteSelection,
        Action reset,
        Action openDialog)
    {
        this.card = card ?? throw new ArgumentNullException(nameof(card));
        this.uploadIcon = uploadIcon ?? throw new ArgumentNullException(nameof(uploadIcon));
        this.hintText = hintText ?? throw new ArgumentNullException(nameof(hintText));
        this.defaultBackground = defaultBackground ?? throw new ArgumentNullException(nameof(defaultBackground));
        this.fileController = fileController ?? throw new ArgumentNullException(nameof(fileController));
        this.openCompleteSelection = openCompleteSelection ?? throw new ArgumentNullException(nameof(openCompleteSelection));
        this.reset = reset ?? throw new ArgumentNullException(nameof(reset));
        this.openDialog = openDialog ?? throw new ArgumentNullException(nameof(openDialog));
    }

    public void DragEnter(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Copy;
        card.Background = new SolidColorBrush(Color.FromArgb(30, 103, 80, 164));
        uploadIcon.Kind = PackIconKind.CloudDownload;
        hintText.Text = "松开以导入文件";
    }

    public void DragLeave()
    {
        card.Background = defaultBackground;
        uploadIcon.Kind = PackIconKind.FileUpload;
        hintText.Text = "拖放 .png、_anim、_build 文件到此处";
    }

    public void Drop(DragEventArgs e)
    {
        DragLeave();
        if (!e.Data.GetDataPresent(DataFormats.FileDrop) ||
            e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return;
        }

        var selection = fileController.AddFiles(paths);
        if (selection.IsComplete)
        {
            openCompleteSelection(selection);
        }
    }

    public void DoubleClick()
    {
        if (fileController.HasAnyFile)
        {
            reset();
        }
        else
        {
            openDialog();
        }
    }

    public void MouseEnter()
    {
        card.ToolTip = fileController.HasAnyFile ? "双击清空内容" : "双击选择文件";
    }
}
