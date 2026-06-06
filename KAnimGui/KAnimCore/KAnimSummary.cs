using System.Linq;
using KanimLib;

namespace KAnimGui.KAnimCore
{
    public sealed record KAnimSummary(
        string TextureSummary,
        string BuildSummary,
        string AnimSummary)
    {
        public static KAnimSummary FromPackage(KAnimPackage package)
        {
            var textureSummary = package.Texture == null
                ? "未加载"
                : $"{package.Texture.PixelWidth} x {package.Texture.PixelHeight}";

            var buildSummary = package.Build == null
                ? "未加载"
                : $"{package.Build.Symbols.Count} symbols, {package.Build.Symbols.Sum(symbol => symbol.Frames.Count)} frames, version {package.Build.Version}";

            var animSummary = package.Anim == null
                ? "未加载"
                : $"{package.Anim.Banks.Count} banks, {package.Anim.Banks.Sum(bank => bank.Frames.Count)} frames, {package.Anim.Banks.SelectMany(bank => bank.Frames).Sum(frame => frame.Elements.Count)} elements, version {package.Anim.Version}";

            return new KAnimSummary(textureSummary, buildSummary, animSummary);
        }
    }
}
