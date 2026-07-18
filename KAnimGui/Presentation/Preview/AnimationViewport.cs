using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KAnimGui.Presentation.Preview;

/// <summary>
/// Dedicated preview surface for zooming, panning, and bitmap presentation.
/// The image and the coordinate grid share the same 768-unit canvas transform
/// while the grid itself fills the complete viewport.
/// </summary>
public sealed class AnimationViewport : Grid
{
    private const double CanvasSize = 768;
    // Keep the 768-unit animation canvas centered, but leave more surrounding
    // coordinate space visible in the default viewport.
    private const double DefaultViewSize = 1024;
    // KAnim preview coordinates use 100-unit sub-cells, while one ONI world
    // cell spans 2 x 2 of those sub-cells.
    private const double GridSubCellSize = 100;
    private const double GameCellSize = GridSubCellSize * 2;
    // Keep the coordinate grid focused around the origin. Eight ONI cells
    // across means four cells on either side of the animation origin.
    private const int VisibleGameGridSize = 8;
    private const double VisibleGridHalfExtent = GameCellSize * VisibleGameGridSize / 2;
    private static readonly Color DefaultBackground = Color.FromRgb(150, 150, 150);
    private static readonly Color DarkBackground = Color.FromRgb(32, 36, 43);
    private static readonly Color DarkGridLine = Color.FromArgb(70, 255, 255, 255);
    private static readonly Color DarkGameGridLine = Color.FromArgb(135, 255, 255, 255);
    private static readonly Color DarkOriginLine = Color.FromArgb(200, 255, 238, 100);
    private static readonly Color LightGridLine = Color.FromArgb(90, 90, 90, 90);
    private static readonly Color LightGameGridLine = Color.FromArgb(145, 70, 70, 70);
    private static readonly Color LightOriginLine = Color.FromArgb(210, 126, 54, 82);

    private readonly Canvas surface;
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
        // A transparent background keeps the whole preview surface hit-testable,
        // including the empty space around the animation.
        Background = Brushes.Transparent;
        // Keyboard shortcuts are handled by the preview window's root. Keeping
        // this surface non-focusable prevents WPF's blue focus adorner from
        // being mistaken for a texture clipping frame in compact windows.
        Focusable = false;
        FocusVisualStyle = null;

        surface = new Canvas
        {
            Width = CanvasSize,
            Height = CanvasSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5),
            ClipToBounds = false,
            Focusable = false,
            FocusVisualStyle = null
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
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Stretch = Stretch.Fill,
            IsHitTestVisible = false,
            Focusable = false,
            FocusVisualStyle = null,
            Clip = null
        };
        // The atlas contains very thin white outlines. LowQuality sampling
        // drops those lines as the window crosses fractional scale factors,
        // which looks like the texture is being cropped during resize.
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        // Do not keep a size-specific bitmap cache while the viewport is
        // resizing; let WPF resample the complete source for each layout size.
        RenderOptions.SetCachingHint(image, CachingHint.Unspecified);

        surface.Children.Add(image);
        Children.Add(gridLayer);
        Children.Add(surface);
        SizeChanged += (_, _) => UpdateSurfaceLayout();
        UpdateSurfaceLayout();
        UpdateGridTransform();

        MouseWheel += OnMouseWheel;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        KeyDown += OnKeyDown;
    }

    public ImageSource? ImageSource
    {
        get => image.Source;
        set
        {
            image.Source = value;
            // Lay the source out explicitly inside the fixed canvas. Using a
            // Canvas with a pixel-derived destination rectangle avoids the
            // WPF Image/Uniform arrangement path dropping the left edge of
            // wide atlas textures in compact windows.
            UpdateImageLayout();
            UpdateSurfaceLayout();
        }
    }

    public double Zoom => zoomTransform.ScaleX;

    public void SetZoom(double value)
    {
        double zoom = Math.Clamp(value, 0.25, 6.0);
        zoomTransform.ScaleX = zoom;
        zoomTransform.ScaleY = zoom;
        UpdateGridTransform();
    }

    public void ResetTransform()
    {
        SetZoom(1.0);
        translateTransform.X = 0;
        translateTransform.Y = 0;
        UpdateGridTransform();
    }

    /// <summary>
    /// Sets the animation origin in the renderer's 768x768 canvas coordinates.
    /// </summary>
    public void SetAnimationOrigin(Point origin)
    {
        animationOrigin = origin;
        UpdateGridTransform();
    }

    public void SetBackground(bool dark)
    {
        darkBackground = dark;
        gridLayer.SetPalette(
            dark ? DarkBackground : DefaultBackground,
            dark ? DarkGridLine : LightGridLine,
            dark ? DarkGameGridLine : LightGameGridLine,
            dark ? DarkOriginLine : LightOriginLine);
    }

    private void UpdateSurfaceLayout()
    {
        double width = Math.Max(1, ActualWidth);
        double height = Math.Max(1, ActualHeight);
        double fit = Math.Max(0.001, Math.Min(width / DefaultViewSize, height / DefaultViewSize));
        fitTransform.ScaleX = fit;
        fitTransform.ScaleY = fit;

        // Keep the render surface as a fixed 768-unit canvas. The grid is a
        // separate full-viewport layer, so enlarging this surface to fill the
        // shorter dimension only creates an extra layout clipping boundary for
        // wide textures when the window is resized.
        surface.Width = CanvasSize;
        surface.Height = CanvasSize;
        UpdateGridTransform();
    }

    private void UpdateImageLayout()
    {
        if (image.Source is not BitmapSource source ||
            source.PixelWidth <= 0 ||
            source.PixelHeight <= 0)
        {
            image.Width = CanvasSize;
            image.Height = CanvasSize;
            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, 0);
            return;
        }

        image.Width = CanvasSize;
        image.Height = CanvasSize * source.PixelHeight / source.PixelWidth;
        Canvas.SetLeft(image, 0);
        Canvas.SetTop(image, (CanvasSize - image.Height) / 2);
    }

    private void UpdateGridTransform()
    {
        double left = Math.Max(0, (surface.Width - CanvasSize) / 2);
        double top = Math.Max(0, (surface.Height - CanvasSize) / 2);
        double scale = fitTransform.ScaleX * zoomTransform.ScaleX;
        Point viewportCenter = new(ActualWidth / 2, ActualHeight / 2);
        Point localOrigin = new(left + animationOrigin.X, top + animationOrigin.Y);
        Point surfaceCenter = new(surface.Width / 2, surface.Height / 2);
        gridLayer.Origin = new Point(
            viewportCenter.X + (localOrigin.X - surfaceCenter.X) * scale + translateTransform.X,
            viewportCenter.Y + (localOrigin.Y - surfaceCenter.Y) * scale + translateTransform.Y);
        gridLayer.GridScale = scale;
        gridLayer.SetPalette(
            darkBackground ? DarkBackground : DefaultBackground,
            darkBackground ? DarkGridLine : LightGridLine,
            darkBackground ? DarkGameGridLine : LightGameGridLine,
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

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.H && !e.IsRepeat)
        {
            ResetTransform();
            e.Handled = true;
        }
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
        UpdateGridTransform();
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
        private Color gameGridLine = LightGameGridLine;
        private Color originLine = LightOriginLine;

        public Point Origin { get; set; } = new(CanvasSize / 2, CanvasSize / 2);

        public double GridScale { get; set; } = 1;

        public void SetPalette(
            Color backgroundColor,
            Color gridLineColor,
            Color gameGridLineColor,
            Color originLineColor)
        {
            background = backgroundColor;
            gridLine = gridLineColor;
            gameGridLine = gameGridLineColor;
            originLine = originLineColor;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            double width = Math.Max(RenderSize.Width, 1);
            double height = Math.Max(RenderSize.Height, 1);
            drawingContext.DrawRectangle(new SolidColorBrush(background), null, new Rect(0, 0, width, height));

            double scale = Math.Max(GridScale, 0.001);
            var gridPen = new Pen(new SolidColorBrush(gridLine), Math.Max(0.75, scale));
            var gameGridPen = new Pen(new SolidColorBrush(gameGridLine), Math.Max(1, scale * 1.35));
            DrawVerticalLines(drawingContext, gridPen, gameGridPen, width, height, scale);
            DrawHorizontalLines(drawingContext, gridPen, gameGridPen, width, height, scale);

            var originPen = new Pen(new SolidColorBrush(originLine), 1.5);
            drawingContext.DrawLine(originPen, new Point(Origin.X, 0), new Point(Origin.X, height));
            drawingContext.DrawLine(originPen, new Point(0, Origin.Y), new Point(width, Origin.Y));
        }

        private void DrawVerticalLines(
            DrawingContext drawingContext,
            Pen pen,
            Pen gameGridPen,
            double width,
            double height,
            double scale)
        {
            double extent = VisibleGridHalfExtent * scale;
            double minX = Math.Max(0, Origin.X - extent);
            double maxX = Math.Min(width, Origin.X + extent);
            double minY = Math.Max(0, Origin.Y - extent);
            double maxY = Math.Min(height, Origin.Y + extent);
            DrawLines(
                drawingContext,
                pen,
                minX,
                maxX,
                x => new Point(x, minY),
                x => new Point(x, maxY),
                GridSubCellSize * scale,
                Origin.X);
            DrawLines(
                drawingContext,
                gameGridPen,
                minX,
                maxX,
                x => new Point(x, minY),
                x => new Point(x, maxY),
                GameCellSize * scale,
                Origin.X);
        }

        private void DrawHorizontalLines(
            DrawingContext drawingContext,
            Pen pen,
            Pen gameGridPen,
            double width,
            double height,
            double scale)
        {
            double extent = VisibleGridHalfExtent * scale;
            double minY = Math.Max(0, Origin.Y - extent);
            double maxY = Math.Min(height, Origin.Y + extent);
            double minX = Math.Max(0, Origin.X - extent);
            double maxX = Math.Min(width, Origin.X + extent);
            DrawLines(
                drawingContext,
                pen,
                minY,
                maxY,
                y => new Point(minX, y),
                y => new Point(maxX, y),
                GridSubCellSize * scale,
                Origin.Y);
            DrawLines(
                drawingContext,
                gameGridPen,
                minY,
                maxY,
                y => new Point(minX, y),
                y => new Point(maxX, y),
                GameCellSize * scale,
                Origin.Y);
        }

        private void DrawLines(
            DrawingContext drawingContext,
            Pen pen,
            double minPosition,
            double maxPosition,
            Func<double, Point> startPoint,
            Func<double, Point> endPoint,
            double spacing,
            double originCoordinate)
        {
            double first = originCoordinate - Math.Ceiling(originCoordinate / spacing) * spacing;
            if (first < minPosition)
            {
                first += Math.Ceiling((minPosition - first) / spacing) * spacing;
            }

            for (double position = first; position <= maxPosition + 0.01; position += spacing)
            {
                if (Math.Abs(position - originCoordinate) > 0.01)
                {
                    drawingContext.DrawLine(pen, startPoint(position), endPoint(position));
                }
            }
        }
    }
}
