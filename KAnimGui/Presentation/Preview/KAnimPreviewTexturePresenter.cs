using Rectangle = System.Drawing.Rectangle;
using PointF = System.Drawing.PointF;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KanimLib;

namespace KAnimGui.Presentation.Preview;

/// <summary>
/// Composes a texture bitmap with frame/pivot overlays for the preview viewport.
/// </summary>
public sealed class KAnimPreviewTexturePresenter
{
    private static Typeface Typeface
    {
        get
        {
            var family = System.Windows.Application.Current?.TryFindResource("AppFontFamily") as FontFamily ??
                new FontFamily("HarmonyOS Sans SC, Microsoft YaHei UI, Segoe UI");
            return new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        }
    }

    public void Show(
        AnimationViewport viewport,
        BitmapImage? image,
        KBuild? build,
        Rectangle[]? frames = null,
        PointF[]? pivots = null)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        if (image is null)
        {
            viewport.ImageSource = null;
            return;
        }

        viewport.SetAnimationOrigin(new System.Windows.Point(384, 384));

        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawImage(image, new Rect(0, 0, image.PixelWidth, image.PixelHeight));

            if (frames is not null)
            {
                var redPen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.Red, 2);
                foreach (var frame in frames)
                {
                    if (frame != Rectangle.Empty)
                    {
                        dc.DrawRectangle(null, redPen, new Rect(frame.Left, frame.Top, frame.Width, frame.Height));
                    }
                }
            }

            if (pivots is not null)
            {
                foreach (var pivot in pivots)
                {
                    if (pivot != PointF.Empty)
                    {
                        dc.DrawRectangle(System.Windows.Media.Brushes.LimeGreen, null, new Rect(pivot.X - 1.5, pivot.Y - 1.5, 3, 3));
                    }
                }
            }

            if (build?.NeedsRepack == true)
            {
                var text = new FormattedText(
                    "Requires Rebuild",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface,
                    14,
                    System.Windows.Media.Brushes.Orange,
                    1.0);
                dc.DrawText(text, new System.Windows.Point(5, 5));
            }
        }

        var bitmap = new RenderTargetBitmap(
            image.PixelWidth,
            image.PixelHeight,
            image.DpiX,
            image.DpiY,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        viewport.ImageSource = bitmap;
    }
}
