using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KAnimGui.Presentation.Preview;

/// <summary>
/// Dedicated preview surface for zooming, panning, and bitmap presentation.
/// The image and the coordinate grid live on the same 768-unit canvas so they
/// always share the exact same fit, zoom, and pan transforms.
/// </summary>
public sealed class AnimationViewport : Grid
{
    private const double CanvasSize = 768;
    private const double GridCellSize = 100;
    private static readonly Color DefaultBackground = Color.FromRgb(150, 150, 150);
    private static readonly Color DarkBackground = Color.FromRgb(32, 36, 43);
    private static readonly Color DarkGridLine = Color.FromArgb(90, 255, 255, 255);
    private static readonly Color DarkOriginLine = Color.FromArgb(200, 255, 238, 100);
    private static readonly Color LightGridLine = Color.FromArgb(105, 90, 90, 90);
    private static readonly Color LightOriginLine = Color.FromArgb(210, 126, 54, 82);

    private readonly Grid surface;
    private readonly PreviewGridLayer gridLayer;
    private readonly Image image;
    private readonly ScaleTransform fitTransform;
    private readonly ScaleTransform zoomTransform;
    private readonly TranslateTransform translateTransform;
    private bool isPanning;
    private Point lastPanPoint;
    private Point animationOrigin = new(CanvasSize / 2, CanvasSize / 2);
    private bool darkBackground;

    public AnimationViewport()
    {
        ClipToBounds = true;

        surface = new Grid
        {
            Width = CanvasSize,
            Height = CanvasSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        fitTransform = new ScaleTransform(1, 1);
        zoomTransform = new ScaleTransform(1, 1);
        translateTransform = new TranslateTransform();
        surface.RenderTransform = new TransformGroup
        {
            Children = { fitTransform, zoomTransform, translateTransform }
        };

        gridLayer = new PreviewGridLayer
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false
        };
        image = new Image
        {
            Width = CanvasSize,
            Height = CanvasSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Stretch = Stretch.Uniform
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.LowQuality);
        RenderOptions.SetCachingHint(image, CachingHint.Cache);

        surface.Children.Add(gridLayer);
        surface.Children.Add(image);
        Children.Add(surface);
        SizeChanged += (_, _) => UpdateSurfaceLayout();
        UpdateSurfaceLayout();
        UpdateGridOrigin();

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

    public double Zoom => zoomTransform.ScaleX;

    public void SetZoom(double value)
    {
        double zoom = Math.Clamp(value, 0.25, 6.0);
        zoomTransform.ScaleX = zoom;
        zoomTransform.ScaleY = zoom;
    }

    public void ResetTransform()
    {
        SetZoom(1.0);
        translateTransform.X = 0;
        translateTransform.Y = 0;
    }

    /// <summary>
    /// Sets the animation origin in the renderer's 768x768 canvas coordinates.
    /// </summary>
    public void SetAnimationOrigin(Point origin)
    {
        animationOrigin = origin;
        UpdateGridOrigin();
    }

    public void SetBackground(bool dark)
    {
        darkBackground = dark;
        gridLayer.SetPalette(
            dark ? DarkBackground : DefaultBackground,
            dark ? DarkGridLine : LightGridLine,
            dark ? DarkOriginLine : LightOriginLine);
    }

    private void UpdateSurfaceLayout()
    {
        double width = Math.Max(1, ActualWidth);
        double height = Math.Max(1, ActualHeight);
        double fit = Math.Max(0.001, Math.Min(width / CanvasSize, height / CanvasSize));
        fitTransform.ScaleX = fit;
        fitTransform.ScaleY = fit;

        // The local surface is enlarged in the shorter dimension so the grid
        // covers the whole viewport while the 768x768 animation remains centered.
        surface.Width = width / fit;
        surface.Height = height / fit;
        UpdateGridOrigin();
    }

    private void UpdateGridOrigin()
    {
        double left = Math.Max(0, (surface.Width - CanvasSize) / 2);
        double top = Math.Max(0, (surface.Height - CanvasSize) / 2);
        gridLayer.Origin = new Point(left + animationOrigin.X, top + animationOrigin.Y);
        gridLayer.SetPalette(
            darkBackground ? DarkBackground : DefaultBackground,
            darkBackground ? DarkGridLine : LightGridLine,
            darkBackground ? DarkOriginLine : LightOriginLine);
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

    private sealed class PreviewGridLayer : FrameworkElement
    {
        private Color background = DefaultBackground;
        private Color gridLine = LightGridLine;
        private Color originLine = LightOriginLine;

        public Point Origin { get; set; } = new(CanvasSize / 2, CanvasSize / 2);

        public void SetPalette(Color backgroundColor, Color gridLineColor, Color originLineColor)
        {
            background = backgroundColor;
            gridLine = gridLineColor;
            originLine = originLineColor;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            double width = Math.Max(RenderSize.Width, 1);
            double height = Math.Max(RenderSize.Height, 1);
            drawingContext.DrawRectangle(new SolidColorBrush(background), null, new Rect(0, 0, width, height));

            var gridPen = new Pen(new SolidColorBrush(gridLine), 1);
            DrawVerticalLines(drawingContext, gridPen, width, height);
            DrawHorizontalLines(drawingContext, gridPen, width, height);

            var originPen = new Pen(new SolidColorBrush(originLine), 1.5);
            drawingContext.DrawLine(originPen, new Point(Origin.X, 0), new Point(Origin.X, height));
            drawingContext.DrawLine(originPen, new Point(0, Origin.Y), new Point(width, Origin.Y));
        }

        private void DrawVerticalLines(DrawingContext drawingContext, Pen pen, double width, double height)
        {
            double first = Origin.X - Math.Ceiling(Origin.X / GridCellSize) * GridCellSize;
            for (double x = first; x <= width; x += GridCellSize)
            {
                if (Math.Abs(x - Origin.X) > 0.01)
                {
                    drawingContext.DrawLine(pen, new Point(x, 0), new Point(x, height));
                }
            }
        }

        private void DrawHorizontalLines(DrawingContext drawingContext, Pen pen, double width, double height)
        {
            double first = Origin.Y - Math.Ceiling(Origin.Y / GridCellSize) * GridCellSize;
            for (double y = first; y <= height; y += GridCellSize)
            {
                if (Math.Abs(y - Origin.Y) > 0.01)
                {
                    drawingContext.DrawLine(pen, new Point(0, y), new Point(width, y));
                }
            }
        }
    }
}
