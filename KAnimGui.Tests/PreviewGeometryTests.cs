using KAnimGui.Core.Preview;

namespace KAnimGui.Tests;

public sealed class PreviewGeometryTests
{
    [Fact]
    public void LocalRect_IsCenteredOnPivot()
    {
        PreviewRect rect = PreviewGeometry.GetBuildFrameLocalRect(4, 6);

        Assert.Equal(-4, rect.Left);
        Assert.Equal(-6, rect.Top);
        Assert.Equal(8, rect.Width);
        Assert.Equal(12, rect.Height);
    }

    [Fact]
    public void Transform_UsesPivotAndElementMatrix()
    {
        PreviewPoint point = PreviewGeometry.TransformExplorerPoint(
            2,
            4,
            10,
            20,
            1,
            0,
            0,
            1,
            3,
            5);

        Assert.Equal(14, point.X);
        Assert.Equal(27, point.Y);
    }

    [Fact]
    public void TransformBounds_EnclosesAllCorners()
    {
        PreviewRect rect = new(-1, -2, 2, 4);
        var transform = new PreviewAffineTransform(0, 1, -1, 0, 10, 20);

        PreviewRect result = PreviewGeometry.TransformBounds(rect, transform);

        Assert.Equal(8, result.Left);
        Assert.Equal(19, result.Top);
        Assert.Equal(4, result.Width);
        Assert.Equal(2, result.Height);
    }

    [Fact]
    public void Scale_FitsWithinCanvasMargin()
    {
        Assert.Equal(0.72, PreviewGeometry.CalculateAnimationScale(new PreviewRect(0, 0, 768, 768), 768), 6);
        Assert.Equal(1, PreviewGeometry.CalculateAnimationScale(new PreviewRect(0, 0, 0, 10), 768));
    }
}
