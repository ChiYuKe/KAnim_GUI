using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KAnimGui.Core.Collections;
using KAnimGui.Core.Kanim;
using KAnimGui.Core.Preview;
using KanimLib;
using Brushes = System.Windows.Media.Brushes;
using Rectangle = System.Drawing.Rectangle;

namespace KAnimGui.Presentation.Preview;

public readonly record struct PreviewRenderOptions(
    bool ShowOrigin,
    bool ShowBounds,
    bool HighlightElement,
    int SelectedElementIndex)
{
    public bool HasInspectionOverlay => ShowOrigin || ShowBounds || (HighlightElement && SelectedElementIndex >= 0);
}

/// <summary>
/// WPF renderer and bounded image caches for animation frames.
/// </summary>
public sealed class KAnimPreviewRenderService
{
    private const int AnimationCanvasSize = 768;
    private const int MaxCachedAnimationFrames = 8;
    private const int MaxCachedElementImages = 256;
    private readonly BoundedLruCache<(KAnimBank Bank, int FrameIndex), BitmapSource> animationFrameCache = new(MaxCachedAnimationFrames);
    private readonly BoundedLruCache<KAnimBank, Rect> animationBoundsCache = new(MaxCachedAnimationFrames);
    private readonly BoundedLruCache<string, BitmapSource> elementImageCache = new(MaxCachedElementImages);
    private KAnimPackage? data;

    public void SetData(KAnimPackage? package)
    {
        data = package;
        ClearCaches();
    }

    public BitmapSource RenderAnimationFrame(
        KAnimBank bank,
        int frameIndex,
        PreviewRenderOptions options)
    {
        if (frameIndex < 0 || frameIndex >= bank.Frames.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex));
        }

        var cacheKey = (bank, frameIndex);
        if (!options.HasInspectionOverlay && animationFrameCache.TryGet(cacheKey, out var cached))
        {
            return cached;
        }

        BitmapSource rendered = RenderAnimationFrame(bank.Frames[frameIndex], options);
        if (!options.HasInspectionOverlay)
        {
            animationFrameCache.Set(cacheKey, rendered);
        }

        return rendered;
    }

    public void ClearCaches()
    {
        animationFrameCache.Clear();
        animationBoundsCache.Clear();
        elementImageCache.Clear();
    }

    private BitmapSource RenderAnimationFrame(KAnimFrame animFrame, PreviewRenderOptions options)
    {
        const int canvasSize = AnimationCanvasSize;
        const double center = AnimationCanvasSize / 2.0;
        // Spriter keeps one stable coordinate system for the whole animation.
        // Fitting each frame independently makes the artwork jump whenever its
        // bounds change, which is especially visible on working/building loops.
        var contentBounds = GetAnimationContentBounds(animFrame.Parent);
        var scale = PreviewGeometry.CalculateAnimationScale(
            new PreviewRect(contentBounds.Left, contentBounds.Top, contentBounds.Width, contentBounds.Height),
            canvasSize);
        var offsetX = center - (contentBounds.Left + contentBounds.Width / 2.0) * scale;
        var offsetY = center - (contentBounds.Top + contentBounds.Height / 2.0) * scale;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, canvasSize, canvasSize));

            for (int i = animFrame.Elements.Count - 1; i >= 0; i--)
            {
                DrawAnimationElement(dc, animFrame.Elements[i], offsetX, offsetY, scale);
            }

            if (options.HasInspectionOverlay)
            {
                DrawInspectionOverlay(dc, animFrame, options, offsetX, offsetY, scale);
            }
        }

        var rtb = new RenderTargetBitmap(canvasSize, canvasSize, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    private Rect GetAnimationContentBounds(KAnimBank bank)
    {
        if (animationBoundsCache.TryGet(bank, out var cached))
        {
            return cached;
        }

        Rect? bounds = null;
        foreach (var frame in bank.Frames)
        {
            if (frame.Elements.Count == 0)
            {
                continue;
            }

            Rect frameBounds = CalculateFrameContentBounds(frame);
            bounds = bounds.HasValue ? Rect.Union(bounds.Value, frameBounds) : frameBounds;
        }

        Rect result = bounds ?? new Rect(-50, -50, 100, 100);
        animationBoundsCache.Set(bank, result);
        return result;
    }

    private Rect CalculateFrameContentBounds(KAnimFrame animFrame)
    {
        Rect? bounds = null;
        foreach (var element in animFrame.Elements)
        {
            var elementBounds = CalculateElementBounds(element);
            if (elementBounds == Rect.Empty)
            {
                continue;
            }

            bounds = bounds.HasValue ? Rect.Union(bounds.Value, elementBounds) : elementBounds;
        }

        return bounds ?? new Rect(-50, -50, 100, 100);
    }

    private Rect CalculateElementBounds(KAnimElement element)
    {
        if (data?.Texture == null || data.Build == null)
        {
            return Rect.Empty;
        }

        var buildFrame = KAnimBuildResolver.ResolveFrame(data.Build, element);
        if (buildFrame == null)
        {
            return Rect.Empty;
        }

        var localRect = GetBuildFrameLocalRect(buildFrame);
        var matrix = CreateExplorerElementMatrix(element, buildFrame);
        var topLeft = matrix.Transform(localRect.TopLeft);
        var topRight = matrix.Transform(localRect.TopRight);
        var bottomLeft = matrix.Transform(localRect.BottomLeft);
        var bottomRight = matrix.Transform(localRect.BottomRight);
        var left = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
        var top = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
        var right = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
        var bottom = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));
        return new Rect(new Point(left, top), new Point(right, bottom));
    }

    private void DrawAnimationElement(DrawingContext dc, KAnimElement element, double offsetX, double offsetY, double scale)
    {
        if (data?.Texture == null || data.Build == null)
        {
            return;
        }

        var buildFrame = KAnimBuildResolver.ResolveFrame(data.Build, element);
        if (buildFrame == null)
        {
            return;
        }

        var sourceRect = buildFrame.GetTextureRectangle(data.Texture.PixelWidth, data.Texture.PixelHeight);
        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
        {
            return;
        }

        var cropped = GetCachedElementImage(buildFrame, sourceRect);
        var destination = GetBuildFrameLocalRect(buildFrame);
        dc.PushTransform(new TranslateTransform(offsetX, offsetY));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.PushTransform(new MatrixTransform(CreateExplorerElementMatrix(element, buildFrame)));
        dc.PushOpacity(Math.Clamp(element.Alpha, 0, 1));
        dc.DrawImage(cropped, destination);
        dc.Pop();
        dc.Pop();
        dc.Pop();
        dc.Pop();
    }

    private void DrawInspectionOverlay(
        DrawingContext dc,
        KAnimFrame animFrame,
        PreviewRenderOptions options,
        double offsetX,
        double offsetY,
        double scale)
    {
        dc.PushTransform(new TranslateTransform(offsetX, offsetY));
        dc.PushTransform(new ScaleTransform(scale, scale));

        if (options.ShowBounds)
        {
            var bounds = CalculateFrameContentBounds(animFrame);
            var boundsPen = new Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(72, 210, 255)), 2 / scale)
            {
                DashStyle = DashStyles.Dash
            };
            dc.DrawRectangle(null, boundsPen, bounds);
        }

        if (options.ShowOrigin)
        {
            var axisPen = new Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 238, 100)), 1.5 / scale);
            dc.DrawLine(axisPen, new Point(-28, 0), new Point(28, 0));
            dc.DrawLine(axisPen, new Point(0, -28), new Point(0, 28));
            dc.DrawEllipse(Brushes.Transparent, axisPen, new Point(0, 0), 5 / scale, 5 / scale);
        }

        if (options.HighlightElement &&
            options.SelectedElementIndex >= 0 &&
            options.SelectedElementIndex < animFrame.Elements.Count)
        {
            DrawElementHighlight(dc, animFrame.Elements[options.SelectedElementIndex], scale);
        }

        dc.Pop();
        dc.Pop();
    }

    private void DrawElementHighlight(DrawingContext dc, KAnimElement element, double scale)
    {
        if (data?.Build == null)
        {
            return;
        }

        var buildFrame = KAnimBuildResolver.ResolveFrame(data.Build, element);
        if (buildFrame == null)
        {
            return;
        }

        var localRect = GetBuildFrameLocalRect(buildFrame);
        var highlightPen = new Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 216, 0)), 3 / scale);
        var fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 216, 0));
        dc.PushTransform(new MatrixTransform(CreateExplorerElementMatrix(element, buildFrame)));
        dc.DrawRectangle(fill, highlightPen, localRect);
        dc.Pop();
    }

    private static Rect GetBuildFrameLocalRect(KFrame buildFrame)
    {
        PreviewRect rect = PreviewGeometry.GetBuildFrameLocalRect(buildFrame.PivotWidth, buildFrame.PivotHeight);
        return new Rect(rect.Left, rect.Top, rect.Width, rect.Height);
    }

    private static Matrix CreateExplorerElementMatrix(KAnimElement element, KFrame buildFrame)
    {
        PreviewAffineTransform transform = PreviewGeometry.CreateExplorerElementTransform(
            buildFrame.PivotX,
            buildFrame.PivotY,
            element.M00,
            element.M10,
            element.M01,
            element.M11,
            element.M02,
            element.M12);
        return new Matrix(
            transform.M11,
            transform.M12,
            transform.M21,
            transform.M22,
            transform.OffsetX,
            transform.OffsetY);
    }

    private BitmapSource GetCachedElementImage(KFrame frame, Rectangle sourceRect)
    {
        if (data?.Texture == null)
        {
            throw new InvalidOperationException("Texture is not loaded.");
        }

        var cacheKey = $"{frame.Parent.Hash}:{frame.Index}:{sourceRect.X}:{sourceRect.Y}:{sourceRect.Width}:{sourceRect.Height}";
        if (elementImageCache.TryGet(cacheKey, out var cached))
        {
            return cached;
        }

        var cropped = new CroppedBitmap(data.Texture, new Int32Rect(sourceRect.X, sourceRect.Y, sourceRect.Width, sourceRect.Height));
        cropped.Freeze();
        elementImageCache.Set(cacheKey, cropped);
        return cropped;
    }
}
