using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KAnimGui.Presentation.Preview;

/// <summary>
/// Dedicated preview surface for zooming, panning, and bitmap presentation.
/// </summary>
public sealed class AnimationViewport : Grid
{
    private static readonly Color DarkBackground = Color.FromRgb(72, 72, 72);
    private static readonly Color DarkMinorLine = Color.FromArgb(80, 255, 255, 255);
    private static readonly Color DarkMajorLine = Color.FromArgb(170, 255, 255, 255);
    private static readonly Color LightBackground = Color.FromRgb(232, 232, 232);
    private static readonly Color LightMinorLine = Color.FromArgb(90, 130, 130, 130);
    private static readonly Color LightMajorLine = Color.FromArgb(170, 120, 80, 98);
    private readonly Image image;
    private readonly ScaleTransform scaleTransform;
    private readonly TranslateTransform translateTransform;
    private bool isPanning;
    private Point lastPanPoint;

    public AnimationViewport()
    {
        ClipToBounds = true;
        image = new Image
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Stretch = Stretch.Uniform
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.LowQuality);
        RenderOptions.SetCachingHint(image, CachingHint.Cache);

        scaleTransform = new ScaleTransform(1, 1);
        translateTransform = new TranslateTransform();
        image.RenderTransform = new TransformGroup
        {
            Children = { scaleTransform, translateTransform }
        };
        Children.Add(image);

        MouseWheel += OnMouseWheel;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    public ImageSource? ImageSource
    {
        get => image.Source;
        set => image.Source = value;
    }

    public double Zoom => scaleTransform.ScaleX;

    public void SetZoom(double value)
    {
        double zoom = Math.Clamp(value, 0.25, 6.0);
        scaleTransform.ScaleX = zoom;
        scaleTransform.ScaleY = zoom;
    }

    public void ResetTransform()
    {
        SetZoom(1.0);
        translateTransform.X = 0;
        translateTransform.Y = 0;
    }

    public static DrawingBrush CreateGridBrush(bool dark)
    {
        Color background = dark ? DarkBackground : LightBackground;
        Color minorLine = dark ? DarkMinorLine : LightMinorLine;
        Color majorLine = dark ? DarkMajorLine : LightMajorLine;
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(background),
            null,
            new RectangleGeometry(new Rect(0, 0, 96, 96))));
        group.Children.Add(new GeometryDrawing(
            null,
            new Pen(new SolidColorBrush(minorLine), 1),
            new GeometryGroup
            {
                Children =
                {
                    new LineGeometry(new Point(48, 0), new Point(48, 96)),
                    new LineGeometry(new Point(0, 48), new Point(96, 48))
                }
            }));
        group.Children.Add(new GeometryDrawing(
            null,
            new Pen(new SolidColorBrush(majorLine), 1.4),
            new GeometryGroup
            {
                Children =
                {
                    new LineGeometry(new Point(0, 0), new Point(96, 0)),
                    new LineGeometry(new Point(0, 0), new Point(0, 96))
                }
            }));
        return new DrawingBrush(group)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 96, 96),
            ViewportUnits = BrushMappingMode.Absolute
        };
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        SetZoom(Zoom * (e.Delta > 0 ? 1.12 : 1 / 1.12));
        e.Handled = true;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ResetTransform();
            e.Handled = true;
            return;
        }

        isPanning = true;
        lastPanPoint = e.GetPosition(this);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!isPanning)
        {
            return;
        }

        Point current = e.GetPosition(this);
        Vector delta = current - lastPanPoint;
        translateTransform.X += delta.X;
        translateTransform.Y += delta.Y;
        lastPanPoint = current;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        isPanning = false;
        ReleaseMouseCapture();
    }
}
