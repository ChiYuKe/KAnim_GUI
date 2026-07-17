namespace KAnimGui.Core.Preview;

public readonly record struct PreviewPoint(double X, double Y);

public readonly record struct PreviewRect(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;

    public static PreviewRect FromLTRB(double left, double top, double right, double bottom) =>
        new(left, top, right - left, bottom - top);

    public static PreviewRect Union(PreviewRect first, PreviewRect second)
    {
        double left = Math.Min(first.Left, second.Left);
        double top = Math.Min(first.Top, second.Top);
        double right = Math.Max(first.Right, second.Right);
        double bottom = Math.Max(first.Bottom, second.Bottom);
        return FromLTRB(left, top, right, bottom);
    }
}

public readonly record struct PreviewAffineTransform(
    double M11,
    double M12,
    double M21,
    double M22,
    double OffsetX,
    double OffsetY)
{
    public PreviewPoint Transform(PreviewPoint point) => new(
        point.X * M11 + point.Y * M21 + OffsetX,
        point.X * M12 + point.Y * M22 + OffsetY);
}

public static class PreviewGeometry
{
    public static PreviewRect GetBuildFrameLocalRect(double pivotWidth, double pivotHeight) =>
        new(-pivotWidth, -pivotHeight, pivotWidth * 2.0, pivotHeight * 2.0);

    public static PreviewPoint TransformExplorerPoint(
        double x,
        double y,
        double pivotX,
        double pivotY,
        double m00,
        double m10,
        double m01,
        double m11,
        double m02,
        double m12)
    {
        double pivotedX = x * 0.5 + pivotX;
        double pivotedY = y * 0.5 + pivotY;
        return new PreviewPoint(
            pivotedX * m00 + pivotedY * m01 + m02,
            pivotedX * m10 + pivotedY * m11 + m12);
    }

    public static PreviewAffineTransform CreateExplorerElementTransform(
        double pivotX,
        double pivotY,
        double m00,
        double m10,
        double m01,
        double m11,
        double m02,
        double m12)
    {
        PreviewPoint origin = TransformExplorerPoint(0, 0, pivotX, pivotY, m00, m10, m01, m11, m02, m12);
        PreviewPoint unitX = TransformExplorerPoint(1, 0, pivotX, pivotY, m00, m10, m01, m11, m02, m12);
        PreviewPoint unitY = TransformExplorerPoint(0, 1, pivotX, pivotY, m00, m10, m01, m11, m02, m12);
        return new PreviewAffineTransform(
            unitX.X - origin.X,
            unitX.Y - origin.Y,
            unitY.X - origin.X,
            unitY.Y - origin.Y,
            origin.X,
            origin.Y);
    }

    public static PreviewRect TransformBounds(PreviewRect rect, PreviewAffineTransform transform)
    {
        PreviewPoint topLeft = transform.Transform(new PreviewPoint(rect.Left, rect.Top));
        PreviewPoint topRight = transform.Transform(new PreviewPoint(rect.Right, rect.Top));
        PreviewPoint bottomLeft = transform.Transform(new PreviewPoint(rect.Left, rect.Bottom));
        PreviewPoint bottomRight = transform.Transform(new PreviewPoint(rect.Right, rect.Bottom));

        double left = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
        double top = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
        double right = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
        double bottom = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));
        return PreviewRect.FromLTRB(left, top, right, bottom);
    }

    public static double CalculateAnimationScale(PreviewRect bounds, int canvasSize)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || canvasSize <= 0)
        {
            return 1.0;
        }

        return Math.Min((canvasSize * 0.72) / bounds.Width, (canvasSize * 0.72) / bounds.Height);
    }
}
