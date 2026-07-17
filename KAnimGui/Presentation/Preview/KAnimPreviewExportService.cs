using System.Drawing;
using System.Windows;
using System.Windows.Media.Imaging;
using KanimLib;
using KAnimGui.Core.Kanim;

namespace KAnimGui.Presentation.Preview;

public sealed record KAnimPreviewRegion(Rectangle Rectangle, PointF Pivot);

/// <summary>
/// Applies preview export/replace rules without owning dialog state.
/// </summary>
public sealed class KAnimPreviewExportService
{
    private readonly KAnimPreviewImageService imageService;

    public KAnimPreviewExportService(KAnimPreviewImageService imageService)
    {
        this.imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
    }

    public bool TryResolveRegion(
        KAnimPackage package,
        object selectedObject,
        out KAnimPreviewRegion? region,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(selectedObject);

        if (package.Texture is null)
        {
            region = null;
            error = "当前没有可用的贴图";
            return false;
        }

        KFrame? frame = selectedObject switch
        {
            KFrame selectedFrame => selectedFrame,
            KSymbol symbol when symbol.Frames.Count > 0 => symbol.Frames[0],
            _ => null
        };

        if (frame is null)
        {
            region = null;
            error = selectedObject is KBuild
                ? "导出整张贴图不支持（建议导出 Symbol 或 Frame）"
                : "请选择一个 Frame 或 Symbol 节点";
            return false;
        }

        var rectangle = frame.GetTextureRectangle(package.Texture.PixelWidth, package.Texture.PixelHeight);
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            region = null;
            error = "未找到可导出的区域";
            return false;
        }

        region = new KAnimPreviewRegion(
            rectangle,
            frame.GetPivotPoint(package.Texture.PixelWidth, package.Texture.PixelHeight));
        error = string.Empty;
        return true;
    }

    public void ExportRegion(KAnimPackage package, object selectedObject, string outputPath)
    {
        if (!TryResolveRegion(package, selectedObject, out var region, out var error))
        {
            throw new InvalidOperationException(error);
        }

        var texture = package.Texture ?? throw new InvalidOperationException("当前没有可用的贴图");
        var rectangle = region!.Rectangle;
        imageService.SavePng(
            new CroppedBitmap(texture, new Int32Rect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height)),
            outputPath);
    }

    public BitmapImage ReplaceRegion(
        KAnimPackage package,
        object selectedObject,
        string replacementPath,
        out KAnimPreviewRegion region)
    {
        if (!TryResolveRegion(package, selectedObject, out var resolved, out var error))
        {
            throw new InvalidOperationException(error);
        }

        var texture = package.Texture ?? throw new InvalidOperationException("当前没有可用的贴图");
        region = resolved!;
        var rectangle = region.Rectangle;
        var replaced = imageService.ReplaceRegion(
            texture,
            new Int32Rect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height),
            replacementPath);
        package.Texture = replaced;
        return replaced;
    }
}
