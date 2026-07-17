using System.Windows.Controls;
using MaterialDesignThemes.Wpf;

namespace KAnimGui.Presentation.Preview;

/// <summary>
/// Owns the preview package file selection and its small file-card presentation.
/// </summary>
public sealed class KAnimPreviewFileController
{
    private readonly StackPanel contentPanel;
    private readonly PackIcon uploadIcon;
    private readonly TextBlock hintText;
    private KAnimPreviewFileSelection selection = KAnimPreviewFileSelection.Empty;

    public KAnimPreviewFileController(
        StackPanel contentPanel,
        PackIcon uploadIcon,
        TextBlock hintText)
    {
        this.contentPanel = contentPanel ?? throw new ArgumentNullException(nameof(contentPanel));
        this.uploadIcon = uploadIcon ?? throw new ArgumentNullException(nameof(uploadIcon));
        this.hintText = hintText ?? throw new ArgumentNullException(nameof(hintText));
    }

    public KAnimPreviewFileSelection Selection => selection;
    public bool HasAnyFile => selection.HasAnyFile;
    public bool IsComplete => selection.IsComplete;

    public KAnimPreviewFileSelection SetFiles(IEnumerable<string> paths)
    {
        selection = KAnimPreviewFileSelection.Empty.AddFiles(paths);
        RenderFileList();
        return selection;
    }

    public KAnimPreviewFileSelection AddFiles(IEnumerable<string> paths)
    {
        selection = selection.AddFiles(paths);
        RenderFileList();
        return selection;
    }

    public void Reset()
    {
        selection = KAnimPreviewFileSelection.Empty;
        contentPanel.Children.Clear();
        uploadIcon.Kind = PackIconKind.FileUpload;
        hintText.Text = "拖放 .png、_anim、_build 文件到此处";
        contentPanel.Children.Add(uploadIcon);
        contentPanel.Children.Add(hintText);
    }

    private void RenderFileList()
    {
        contentPanel.Children.Clear();
        var stack = new StackPanel
        {
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            Margin = new System.Windows.Thickness(10, 0, 0, 0)
        };

        foreach (var entry in selection.Entries)
        {
            var grid = new Grid { Margin = new System.Windows.Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new System.Windows.GridLength(24) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = System.Windows.GridLength.Auto });

            var icon = new PackIcon
            {
                Kind = entry.Kind == KAnimPreviewFileKind.Texture
                    ? PackIconKind.FileImageOutline
                    : PackIconKind.FileDocumentOutline,
                Width = 20,
                Height = 20,
                Foreground = System.Windows.Media.Brushes.Gray,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 0, 6, 0)
            };
            Grid.SetColumn(icon, 0);

            var text = new TextBlock
            {
                Text = entry.FileName,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                FontSize = 14,
                TextTrimming = System.Windows.TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(text, 1);
            grid.Children.Add(icon);
            grid.Children.Add(text);
            stack.Children.Add(grid);
        }

        contentPanel.Children.Add(stack);
    }
}
