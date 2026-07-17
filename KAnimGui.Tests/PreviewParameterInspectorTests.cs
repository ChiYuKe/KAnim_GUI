using KAnimGui.Core.Preview;
using KanimLib;

namespace KAnimGui.Tests;

public sealed class PreviewParameterInspectorTests
{
    [Fact]
    public void DescribeFrame_IncludesTextureRectAndPivotWhenDimensionsExist()
    {
        var build = new KBuild();
        var symbol = new KSymbol(build);
        var frame = new KFrame(symbol)
        {
            Index = 3,
            Duration = 2,
            PivotWidth = 20,
            PivotHeight = 10,
            UV_X1 = 0,
            UV_Y1 = 0,
            UV_X2 = 0.5f,
            UV_Y2 = 0.25f
        };

        var values = new PreviewParameterInspector().Describe(frame, 100, 80);

        Assert.Contains(values, item => item.Key == "纹理区域" && item.Value == "0,0,50,20");
        Assert.Contains(values, item => item.Key == "Frame 索引" && item.Value == "3");
    }
}
