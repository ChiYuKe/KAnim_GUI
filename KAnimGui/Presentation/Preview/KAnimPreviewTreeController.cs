using System.Drawing;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using KanimLib;
using KAnimGui.Core.Kanim;
using MaterialDesignThemes.Wpf;

namespace KAnimGui.Presentation.Preview;

/// <summary>
/// Owns TreeView materialization and selection mapping for the previewer.
/// The window only supplies rendering/playback callbacks.
/// </summary>
public sealed class KAnimPreviewTreeController
{
    private readonly TreeView treeView;
    private readonly KAnimTreeModelBuilder modelBuilder;
    private readonly Func<KAnimPackage?> getData;
    private readonly Func<KAnimBank?> getCurrentBank;
    private readonly Action<KAnimBank> selectBank;
    private readonly Action<int, int> selectFrameAndElement;
    private readonly Action startPlayback;
    private readonly Action stopPlayback;
    private readonly Action renderCurrentFrame;
    private readonly Action<BitmapImage?, Rectangle[]?, PointF[]?> updateTexture;
    private readonly Action<object> updateParameters;
    private string searchText = string.Empty;

    public KAnimPreviewTreeController(
        TreeView treeView,
        KAnimTreeModelBuilder modelBuilder,
        Func<KAnimPackage?> getData,
        Func<KAnimBank?> getCurrentBank,
        Action<KAnimBank> selectBank,
        Action<int, int> selectFrameAndElement,
        Action startPlayback,
        Action stopPlayback,
        Action renderCurrentFrame,
        Action<BitmapImage?, Rectangle[]?, PointF[]?> updateTexture,
        Action<object> updateParameters)
    {
        this.treeView = treeView ?? throw new ArgumentNullException(nameof(treeView));
        this.modelBuilder = modelBuilder ?? throw new ArgumentNullException(nameof(modelBuilder));
        this.getData = getData ?? throw new ArgumentNullException(nameof(getData));
        this.getCurrentBank = getCurrentBank ?? throw new ArgumentNullException(nameof(getCurrentBank));
        this.selectBank = selectBank ?? throw new ArgumentNullException(nameof(selectBank));
        this.selectFrameAndElement = selectFrameAndElement ?? throw new ArgumentNullException(nameof(selectFrameAndElement));
        this.startPlayback = startPlayback ?? throw new ArgumentNullException(nameof(startPlayback));
        this.stopPlayback = stopPlayback ?? throw new ArgumentNullException(nameof(stopPlayback));
        this.renderCurrentFrame = renderCurrentFrame ?? throw new ArgumentNullException(nameof(renderCurrentFrame));
        this.updateTexture = updateTexture ?? throw new ArgumentNullException(nameof(updateTexture));
        this.updateParameters = updateParameters ?? throw new ArgumentNullException(nameof(updateParameters));
    }

    public void Refresh(KAnimPackage? data)
    {
        treeView.Items.Clear();
        if (data is null)
        {
            return;
        }

        foreach (var node in modelBuilder.Build(data.Build, data.Anim, searchText))
        {
            var treeItem = CreateTreeNode(node);
            treeItem.IsExpanded = true;
            treeView.Items.Add(treeItem);
        }
    }

    public void SetSearchText(string? value)
    {
        searchText = value?.Trim() ?? string.Empty;
        Refresh(getData());
    }

    public object? GetSelectedValue()
    {
        return treeView.SelectedItem is TreeViewItem item ? GetTreeItemValue(item) : null;
    }

    public void HandleSelectedItemChanged()
    {
        if (treeView.SelectedItem is not TreeViewItem selectedItem || getData() is not { } data)
        {
            return;
        }

        if (selectedItem.Tag is TreeNodeTag nodeTag)
        {
            SyncAnimationSelectionFromTree(nodeTag);
        }

        var selectedObject = GetTreeItemValue(selectedItem);
        var frames = new List<Rectangle>();
        var pivots = new List<PointF>();
        bool showTextureView = true;

        switch (selectedObject)
        {
            case KBuild:
            case KAnim:
                break;
            case KAnimBank:
                showTextureView = false;
                break;
            case KSymbol symbol when data.Texture is not null:
                foreach (var frame in symbol.Frames)
                {
                    frames.Add(frame.GetTextureRectangle(data.Texture.PixelWidth, data.Texture.PixelHeight));
                    pivots.Add(frame.GetPivotPoint(data.Texture.PixelWidth, data.Texture.PixelHeight));
                }
                break;
            case KFrame frame when data.Texture is not null:
                frames.Add(frame.GetTextureRectangle(data.Texture.PixelWidth, data.Texture.PixelHeight));
                pivots.Add(frame.GetPivotPoint(data.Texture.PixelWidth, data.Texture.PixelHeight));
                break;
            case KAnimFrame animFrame when data.Texture is not null && data.Build is not null:
                showTextureView = false;
                AddAnimationFrameRegions(animFrame, data, frames, pivots);
                break;
            case KAnimElement element when data.Texture is not null && data.Build is not null:
                showTextureView = false;
                var resolved = KAnimBuildResolver.ResolveFrame(data.Build, element);
                if (resolved is not null)
                {
                    frames.Add(resolved.GetTextureRectangle(data.Texture.PixelWidth, data.Texture.PixelHeight));
                    pivots.Add(resolved.GetPivotPoint(data.Texture.PixelWidth, data.Texture.PixelHeight));
                }
                break;
        }

        if (data.Texture is not null && showTextureView)
        {
            updateTexture(data.Texture, frames.ToArray(), pivots.ToArray());
        }

        updateParameters(selectedObject);
    }

    public void HandleBankExpanded(object sender)
    {
        if (sender is not TreeViewItem bankNode ||
            bankNode.Tag is not TreeNodeTag { Value: KAnimBank bank })
        {
            return;
        }

        bankNode.Expanded -= BankNode_Expanded;
        bankNode.Items.Clear();
        var data = getData();
        if (data?.Build is null && searchText.Length > 0)
        {
            return;
        }

        foreach (var child in modelBuilder.BuildExpandedBank(bank, data?.Build, searchText))
        {
            bankNode.Items.Add(CreateTreeNode(child));
        }
    }

    private void SyncAnimationSelectionFromTree(TreeNodeTag nodeTag)
    {
        if (nodeTag.Bank is null)
        {
            return;
        }

        if (!ReferenceEquals(getCurrentBank(), nodeTag.Bank))
        {
            selectBank(nodeTag.Bank);
            if (nodeTag.FrameIndex < 0)
            {
                startPlayback();
            }
        }

        if (nodeTag.FrameIndex >= 0)
        {
            selectFrameAndElement(nodeTag.FrameIndex, nodeTag.ElementIndex);
            stopPlayback();
            renderCurrentFrame();
        }
    }

    private void AddAnimationFrameRegions(
        KAnimFrame animationFrame,
        KAnimPackage data,
        List<Rectangle> frames,
        List<PointF> pivots)
    {
        foreach (var element in animationFrame.Elements)
        {
            var frame = KAnimBuildResolver.ResolveFrame(data.Build!, element);
            if (frame is not null)
            {
                frames.Add(frame.GetTextureRectangle(data.Texture!.PixelWidth, data.Texture.PixelHeight));
                pivots.Add(frame.GetPivotPoint(data.Texture.PixelWidth, data.Texture.PixelHeight));
            }
        }
    }

    private TreeViewItem CreateTreeNode(KAnimTreeNode node)
    {
        var treeItem = CreateTreeItem(
            node.Title,
            new TreeNodeTag(node.Value, node.Kind.ToString(), node.Bank, node.FrameIndex, node.ElementIndex),
            GetTreeIcon(node.Kind));

        if (node.IsDeferred)
        {
            treeItem.Items.Add(new TreeViewItem { Header = "展开加载帧..." });
            treeItem.Expanded += BankNode_Expanded;
            treeItem.IsExpanded = searchText.Length > 0;
        }
        else
        {
            foreach (var child in node.Children)
            {
                treeItem.Items.Add(CreateTreeNode(child));
            }
        }

        return treeItem;
    }

    private static PackIconKind GetTreeIcon(KAnimTreeNodeKind kind) => kind switch
    {
        KAnimTreeNodeKind.Build => PackIconKind.ImageMultipleOutline,
        KAnimTreeNodeKind.Symbol => PackIconKind.ShapeOutline,
        KAnimTreeNodeKind.Frame => PackIconKind.CropFree,
        KAnimTreeNodeKind.Anim => PackIconKind.AnimationPlayOutline,
        KAnimTreeNodeKind.Bank => PackIconKind.MovieOpenPlayOutline,
        KAnimTreeNodeKind.AnimFrame => PackIconKind.LayersOutline,
        KAnimTreeNodeKind.Element => PackIconKind.VectorSquare,
        _ => PackIconKind.FileOutline
    };

    private static object GetTreeItemValue(TreeViewItem item) =>
        item.Tag is TreeNodeTag nodeTag ? nodeTag.Value : item.Tag!;

    private static TreeViewItem CreateTreeItem(string title, TreeNodeTag tag, PackIconKind icon)
    {
        var text = new TextBlock
        {
            Text = title,
            TextTrimming = System.Windows.TextTrimming.CharacterEllipsis,
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        };
        var header = new DockPanel { LastChildFill = true };
        header.Children.Add(new PackIcon
        {
            Kind = icon,
            Width = 16,
            Height = 16,
            Margin = new System.Windows.Thickness(0, 0, 6, 0),
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        });
        header.Children.Add(text);
        return new TreeViewItem { Header = header, Tag = tag };
    }

    private void BankNode_Expanded(object sender, System.Windows.RoutedEventArgs e) => HandleBankExpanded(sender);

    private sealed record TreeNodeTag(
        object Value,
        string Type,
        KAnimBank? Bank = null,
        int FrameIndex = -1,
        int ElementIndex = -1);
}
