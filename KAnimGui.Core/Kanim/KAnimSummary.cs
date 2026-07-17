using System.Linq;

namespace KAnimGui.Core.Kanim;

public sealed record KAnimSummary(
    string TextureSummary,
    string BuildSummary,
    string AnimSummary)
{
    public static KAnimSummary FromPackage(KAnimDataPackage package)
    {
        var textureSummary = package.HasTexture
            ? $"{package.TextureWidth} x {package.TextureHeight}"
            : "未加载";
        var buildSummary = package.Build == null
            ? "未加载"
            : $"{package.Build.Symbols.Count} symbols, {package.Build.Symbols.Sum(symbol => symbol.Frames.Count)} frames, version {package.Build.Version}";
        var animSummary = package.Anim == null
            ? "未加载"
            : $"{package.Anim.Banks.Count} banks, {package.Anim.Banks.Sum(bank => bank.Frames.Count)} frames, {package.Anim.Banks.SelectMany(bank => bank.Frames).Sum(frame => frame.Elements.Count)} elements, version {package.Anim.Version}";

        return new KAnimSummary(textureSummary, buildSummary, animSummary);
    }
}
