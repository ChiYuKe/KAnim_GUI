using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KanimLib;

namespace KAnimGui.KAnimCore
{
    public static class KAnimDiagnostics
    {
        public static IReadOnlyList<KAnimDiagnostic> Analyze(KAnimPackage package)
        {
            var diagnostics = new List<KAnimDiagnostic>();

            AnalyzePackage(package, diagnostics);

            if (package.Build != null)
            {
                AnalyzeBuild(package.Build, diagnostics);
            }

            if (package.Anim != null)
            {
                AnalyzeAnim(package.Anim, package.Build, diagnostics);
            }

            if (package.Texture != null && package.Build != null)
            {
                AnalyzeTexture(package.Texture.PixelWidth, package.Texture.PixelHeight, package.Build, diagnostics);
            }

            if (diagnostics.Count == 0)
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Info,
                    "OK",
                    "Package",
                    "未发现明显结构问题。"));
            }

            return diagnostics;
        }

        public static string FormatReport(KAnimPackage package, IReadOnlyList<KAnimDiagnostic> diagnostics)
        {
            var summary = KAnimSummary.FromPackage(package);
            var lines = new List<string>
            {
                "KAnim 诊断报告",
                "",
                $"Texture: {summary.TextureSummary}",
                $"Build: {summary.BuildSummary}",
                $"Anim: {summary.AnimSummary}",
                "",
                "问题列表:"
            };

            foreach (var diagnostic in diagnostics)
            {
                lines.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"[{diagnostic.Severity}] {diagnostic.Code} @ {diagnostic.Location}: {diagnostic.Message}"));
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static void AnalyzePackage(KAnimPackage package, List<KAnimDiagnostic> diagnostics)
        {
            if (!package.HasTexture)
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Warning,
                    "MISSING_TEXTURE",
                    "Package.Texture",
                    "未加载 PNG 贴图，无法检查 UV 对应的实际像素区域。"));
            }

            if (!package.HasBuild)
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Error,
                    "MISSING_BUILD",
                    "Package.Build",
                    "未加载 _build.bytes，无法解析 symbol/frame。"));
            }

            if (!package.HasAnim)
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Warning,
                    "MISSING_ANIM",
                    "Package.Anim",
                    "未加载 _anim.bytes，只能检查 build/texture。"));
            }
        }

        private static void AnalyzeBuild(KBuild build, List<KAnimDiagnostic> diagnostics)
        {
            if (build.Version <= 0 || build.Version > KBuild.CURRENT_BUILD_VERSION)
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Warning,
                    "BUILD_VERSION",
                    "Build.Version",
                    $"Build version {build.Version} 不在常见范围 1..{KBuild.CURRENT_BUILD_VERSION}。"));
            }

            if (build.SymbolCount != build.Symbols.Count)
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Warning,
                    "BUILD_SYMBOL_COUNT",
                    "Build.SymbolCount",
                    $"声明数量 {build.SymbolCount} 与实际 symbol 数量 {build.Symbols.Count} 不一致。"));
            }

            var frameCount = build.Symbols.Sum(symbol => symbol.Frames.Count);
            if (build.FrameCount != frameCount)
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Warning,
                    "BUILD_FRAME_COUNT",
                    "Build.FrameCount",
                    $"声明数量 {build.FrameCount} 与实际 frame 数量 {frameCount} 不一致。"));
            }

            foreach (var symbol in build.Symbols)
            {
                var location = $"Build.Symbol[{symbol.Name}]";

                if (!build.SymbolNames.ContainsKey(symbol.Hash))
                {
                    diagnostics.Add(new KAnimDiagnostic(
                        KAnimDiagnosticSeverity.Warning,
                        "SYMBOL_NAME_MISSING",
                        location,
                        $"Symbol hash {symbol.Hash} 未出现在 SymbolNames 表中。"));
                }

                if (symbol.FrameCount != symbol.Frames.Count)
                {
                    diagnostics.Add(new KAnimDiagnostic(
                        KAnimDiagnosticSeverity.Warning,
                        "SYMBOL_FRAME_COUNT",
                        location,
                        $"声明 frame 数量 {symbol.FrameCount} 与实际数量 {symbol.Frames.Count} 不一致。"));
                }

                foreach (var frame in symbol.Frames)
                {
                    AnalyzeBuildFrame(frame, $"{location}.Frame[{frame.Index}]", diagnostics);
                }
            }
        }

        private static void AnalyzeBuildFrame(KFrame frame, string location, List<KAnimDiagnostic> diagnostics)
        {
            if (frame.PivotWidth <= 0 || frame.PivotHeight <= 0)
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Warning,
                    "FRAME_PIVOT_SIZE",
                    location,
                    $"Pivot 尺寸异常: {frame.PivotWidth} x {frame.PivotHeight}。"));
            }

            if (!IsFinite(frame.PivotX) || !IsFinite(frame.PivotY) ||
                !IsFinite(frame.PivotWidth) || !IsFinite(frame.PivotHeight))
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Error,
                    "FRAME_PIVOT_NAN",
                    location,
                    "Pivot 含 NaN 或 Infinity。"));
            }

            if (!IsFinite(frame.UV_X1) || !IsFinite(frame.UV_Y1) ||
                !IsFinite(frame.UV_X2) || !IsFinite(frame.UV_Y2))
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Error,
                    "FRAME_UV_NAN",
                    location,
                    "UV 含 NaN 或 Infinity。"));
                return;
            }

            if (frame.UV_X1 < 0 || frame.UV_Y1 < 0 || frame.UV_X2 > 1 || frame.UV_Y2 > 1)
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Warning,
                    "FRAME_UV_RANGE",
                    location,
                    $"UV 超出 0..1 范围: ({frame.UV_X1}, {frame.UV_Y1}) - ({frame.UV_X2}, {frame.UV_Y2})。"));
            }

            if (frame.UV_X2 < frame.UV_X1 || frame.UV_Y2 < frame.UV_Y1)
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Error,
                    "FRAME_UV_ORDER",
                    location,
                    "UV 右下角小于左上角，纹理区域无效。"));
            }
        }

        private static void AnalyzeAnim(KAnim anim, KBuild? build, List<KAnimDiagnostic> diagnostics)
        {
            if (anim.Version <= 0)
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Warning,
                    "ANIM_VERSION",
                    "Anim.Version",
                    $"Anim version {anim.Version} 不在常见范围内。"));
            }

            if (anim.BankCount != anim.Banks.Count)
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Warning,
                    "ANIM_BANK_COUNT",
                    "Anim.BankCount",
                    $"声明数量 {anim.BankCount} 与实际 bank 数量 {anim.Banks.Count} 不一致。"));
            }

            var frameCount = anim.Banks.Sum(bank => bank.Frames.Count);
            if (anim.FrameCount != frameCount)
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Warning,
                    "ANIM_FRAME_COUNT",
                    "Anim.FrameCount",
                    $"声明数量 {anim.FrameCount} 与实际 frame 数量 {frameCount} 不一致。"));
            }

            var elementCount = anim.Banks.SelectMany(bank => bank.Frames).Sum(frame => frame.Elements.Count);
            if (anim.ElementCount != elementCount)
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Warning,
                    "ANIM_ELEMENT_COUNT",
                    "Anim.ElementCount",
                    $"声明数量 {anim.ElementCount} 与实际 element 数量 {elementCount} 不一致。"));
            }

            foreach (var bank in anim.Banks)
            {
                AnalyzeBank(bank, build, diagnostics);
            }
        }

        private static void AnalyzeBank(KAnimBank bank, KBuild? build, List<KAnimDiagnostic> diagnostics)
        {
            var bankLocation = $"Anim.Bank[{bank.Name}]";
            if (bank.Rate <= 0 || !IsFinite(bank.Rate))
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Warning,
                    "BANK_RATE",
                    bankLocation,
                    $"动画帧率异常: {bank.Rate}。"));
            }

            if (bank.FrameCount != bank.Frames.Count)
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Warning,
                    "BANK_FRAME_COUNT",
                    bankLocation,
                    $"声明 frame 数量 {bank.FrameCount} 与实际数量 {bank.Frames.Count} 不一致。"));
            }

            for (var frameIndex = 0; frameIndex < bank.Frames.Count; frameIndex++)
            {
                var frame = bank.Frames[frameIndex];
                var frameLocation = $"{bankLocation}.Frame[{frameIndex}]";

                if (frame.ElementCount != frame.Elements.Count)
                {
                    diagnostics.Add(new KAnimDiagnostic(
                        KAnimDiagnosticSeverity.Warning,
                        "ANIM_FRAME_ELEMENT_COUNT",
                        frameLocation,
                        $"声明 element 数量 {frame.ElementCount} 与实际数量 {frame.Elements.Count} 不一致。"));
                }

                foreach (var element in frame.Elements)
                {
                    AnalyzeElement(element, build, $"{frameLocation}.Element[{element.SymbolHash}:{element.FrameNumber}]", diagnostics);
                }
            }
        }

        private static void AnalyzeElement(
            KAnimElement element,
            KBuild? build,
            string location,
            List<KAnimDiagnostic> diagnostics)
        {
            if (build == null)
            {
                return;
            }

            var symbol = build.GetSymbol(element.SymbolHash);
            if (symbol == null)
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Error,
                    "ELEMENT_SYMBOL_MISSING",
                    location,
                    $"Element 引用了不存在的 symbol hash {element.SymbolHash}。"));
                return;
            }

            if (KAnimBuildResolver.ResolveFrame(symbol, element.FrameNumber) == null)
            {
                var lookupCount = symbol.Frames.Count == 0
                    ? 0
                    : symbol.Frames.Max(frame => frame.Index + Math.Max(1, frame.Duration));
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Error,
                    "ELEMENT_FRAME_MISSING",
                    location,
                    $"Element 引用了 {symbol.Name} 的 frame {element.FrameNumber}，但该 symbol 的可查找帧范围是 0..{Math.Max(0, lookupCount - 1)}。"));
            }

            if (element.FolderHash != 0 && element.FolderHash != element.SymbolHash)
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Info,
                    "ELEMENT_FOLDER_HASH",
                    location,
                    $"FolderHash ({element.FolderHash}) 与 SymbolHash ({element.SymbolHash}) 不一致。"));
            }

            if (!IsColorChannel(element.Red) || !IsColorChannel(element.Green) ||
                !IsColorChannel(element.Blue) || !IsColorChannel(element.Alpha))
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Warning,
                    "ELEMENT_COLOR_RANGE",
                    location,
                    $"颜色/Alpha 超出 0..1 范围: R={element.Red}, G={element.Green}, B={element.Blue}, A={element.Alpha}。"));
            }

            if (!IsFinite(element.M00) || !IsFinite(element.M01) || !IsFinite(element.M02) ||
                !IsFinite(element.M10) || !IsFinite(element.M11) || !IsFinite(element.M12))
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Error,
                    "ELEMENT_MATRIX_NAN",
                    location,
                    "变换矩阵含 NaN 或 Infinity。"));
            }
        }

        private static void AnalyzeTexture(
            int textureWidth,
            int textureHeight,
            KBuild build,
            List<KAnimDiagnostic> diagnostics)
        {
            if (textureWidth <= 0 || textureHeight <= 0)
            {
                diagnostics.Add(new KAnimDiagnostic(
                    KAnimDiagnosticSeverity.Error,
                    "TEXTURE_SIZE",
                    "Texture",
                    $"贴图尺寸无效: {textureWidth} x {textureHeight}。"));
                return;
            }

            foreach (var symbol in build.Symbols)
            {
                foreach (var frame in symbol.Frames)
                {
                    var rect = frame.GetTextureRectangle(textureWidth, textureHeight);
                    if (rect.Width <= 0 || rect.Height <= 0)
                    {
                        diagnostics.Add(new KAnimDiagnostic(
                            KAnimDiagnosticSeverity.Error,
                            "FRAME_TEXTURE_RECT",
                            $"Build.Symbol[{symbol.Name}].Frame[{frame.Index}]",
                            $"计算出的纹理区域无效: {rect.X},{rect.Y},{rect.Width},{rect.Height}。"));
                    }
                }
            }
        }

        private static bool IsColorChannel(float value) => value >= 0 && value <= 1 && IsFinite(value);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
