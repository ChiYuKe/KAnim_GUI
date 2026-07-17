using KanimLib;

namespace KAnimGui.Core.Kanim;

/// <summary>
/// Resolves animation elements to build frames without UI or file-system dependencies.
/// </summary>
public static class KAnimBuildResolver
{
    public static KFrame? ResolveFrame(KBuild? build, KAnimElement element)
    {
        var symbol = build?.GetSymbol(element.SymbolHash);
        return symbol == null ? null : ResolveFrame(symbol, element.FrameNumber);
    }

    public static KFrame? ResolveFrame(KSymbol symbol, int sourceFrameNumber)
    {
        return symbol.GetFrame(sourceFrameNumber);
    }

    public static IReadOnlyDictionary<int, KFrame> BuildFrameLookup(KSymbol symbol)
    {
        var lookup = new Dictionary<int, KFrame>();
        foreach (var frame in symbol.Frames)
        {
            var duration = GetLookupDuration(frame);
            for (var sourceFrame = frame.Index; sourceFrame < frame.Index + duration; sourceFrame++)
            {
                lookup[sourceFrame] = frame;
            }
        }

        return lookup;
    }

    private static int GetLookupDuration(KFrame frame) =>
        frame.Duration > 0 ? frame.Duration : 1;
}
