using System.Globalization;
using KanimLib;

namespace KAnimGui.Core.Preview;

public sealed record PreviewParameter(string Key, string Value);

/// <summary>
/// Produces inspectable KAnim values without depending on WPF controls.
/// </summary>
public sealed class PreviewParameterInspector
{
    public IReadOnlyList<PreviewParameter> Describe(
        object? selected,
        int? textureWidth = null,
        int? textureHeight = null)
    {
        var list = new List<PreviewParameter>();
        switch (selected)
        {
            case KBuild build:
                list.Add(new("Build 名称", build.Name));
                list.Add(new("Symbol 数量", build.SymbolCount.ToString(CultureInfo.CurrentCulture)));
                list.Add(new("Frame 总数", build.FrameCount.ToString(CultureInfo.CurrentCulture)));
                break;

            case KSymbol symbol:
                list.Add(new("Symbol 名称", symbol.Name));
                list.Add(new("Hash", symbol.Hash.ToString(CultureInfo.CurrentCulture)));
                list.Add(new("帧数量", symbol.Frames.Count.ToString(CultureInfo.CurrentCulture)));
                break;

            case KFrame frame:
                list.Add(new("Frame 索引", frame.Index.ToString(CultureInfo.CurrentCulture)));
                list.Add(new("持续时间(ms)", frame.Duration.ToString(CultureInfo.CurrentCulture)));
                if (textureWidth is > 0 && textureHeight is > 0)
                {
                    var rect = frame.GetTextureRectangle(textureWidth.Value, textureHeight.Value);
                    list.Add(new("纹理区域", $"{rect.X},{rect.Y},{rect.Width},{rect.Height}"));
                    var pivot = frame.GetPivotPoint(textureWidth.Value, textureHeight.Value);
                    list.Add(new("锚点", $"{pivot.X:F2},{pivot.Y:F2}"));
                }
                break;

            case KAnimFrame animFrame:
                list.Add(new("X", animFrame.X.ToString("F2", CultureInfo.CurrentCulture)));
                list.Add(new("Y", animFrame.Y.ToString("F2", CultureInfo.CurrentCulture)));
                list.Add(new("宽度", animFrame.Width.ToString("F2", CultureInfo.CurrentCulture)));
                list.Add(new("高度", animFrame.Height.ToString("F2", CultureInfo.CurrentCulture)));
                list.Add(new("元素数量", animFrame.ElementCount.ToString(CultureInfo.CurrentCulture)));
                break;

            case KAnimElement element:
                list.Add(new("SymbolHash", element.SymbolHash.ToString(CultureInfo.CurrentCulture)));
                list.Add(new("FrameNumber", element.FrameNumber.ToString(CultureInfo.CurrentCulture)));
                list.Add(new("FolderHash", element.FolderHash.ToString(CultureInfo.CurrentCulture)));
                list.Add(new("Flags", element.Flags.ToString()));
                list.Add(new("R", element.Red.ToString("F2", CultureInfo.CurrentCulture)));
                list.Add(new("G", element.Green.ToString("F2", CultureInfo.CurrentCulture)));
                list.Add(new("B", element.Blue.ToString("F2", CultureInfo.CurrentCulture)));
                list.Add(new("A", element.Alpha.ToString("F2", CultureInfo.CurrentCulture)));
                list.Add(new("M00", element.M00.ToString("F2", CultureInfo.CurrentCulture)));
                list.Add(new("M10", element.M10.ToString("F2", CultureInfo.CurrentCulture)));
                list.Add(new("M01", element.M01.ToString("F2", CultureInfo.CurrentCulture)));
                list.Add(new("M11", element.M11.ToString("F2", CultureInfo.CurrentCulture)));
                list.Add(new("M02", element.M02.ToString("F2", CultureInfo.CurrentCulture)));
                list.Add(new("M12", element.M12.ToString("F2", CultureInfo.CurrentCulture)));
                list.Add(new("Unused", element.Unused.ToString("F2", CultureInfo.CurrentCulture)));
                break;

            default:
                list.Add(new("无可用参数信息", string.Empty));
                break;
        }

        return list;
    }
}
