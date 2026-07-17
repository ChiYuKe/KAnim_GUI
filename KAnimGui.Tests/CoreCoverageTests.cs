using KAnimGui.Core.Kanim;
using KAnimGui.Core.Preview;
using KanimLib;

namespace KAnimGui.Tests;

public sealed class CoreCoverageTests
{
    [Fact]
    public void DataPackageFlagsAndSummaryHandleEmptyAndCompletePackages()
    {
        var empty = new KAnimDataPackage(null, null);
        Assert.False(empty.HasAnyData);
        Assert.False(empty.IsComplete);
        var emptySummary = KAnimSummary.FromPackage(empty);
        Assert.Equal("未加载", emptySummary.TextureSummary);
        Assert.Equal("未加载", emptySummary.BuildSummary);
        Assert.Equal("未加载", emptySummary.AnimSummary);

        var build = new KBuild { Version = 10 };
        var symbol = new KSymbol(build) { Hash = 7 };
        build.Symbols.Add(symbol);
        build.SymbolNames[symbol.Hash] = "body";
        symbol.Frames.Add(new KFrame(symbol));
        var anim = new KAnim { Version = 5 };
        var bank = new KAnimBank(anim) { Name = "idle" };
        var animFrame = new KAnimFrame(bank);
        bank.Frames.Add(animFrame);
        anim.Banks.Add(bank);
        var texture = new KAnimTextureData(64, 32, new byte[] { 1 });
        var complete = new KAnimPackageData(build, anim, texture);

        Assert.True(complete.HasAnyData);
        Assert.True(complete.HasBuild);
        Assert.True(complete.HasAnim);
        Assert.True(complete.HasTexture);
        var summary = KAnimSummary.FromPackage(new KAnimDataPackage(build, anim, 64, 32));
        Assert.Equal("64 x 32", summary.TextureSummary);
        Assert.Contains("1 symbols", summary.BuildSummary);
        Assert.Contains("1 banks", summary.AnimSummary);
    }

    [Fact]
    public void ParameterInspectorDescribesEverySupportedSelection()
    {
        var build = new KBuild { Name = "sample", SymbolCount = 1, FrameCount = 1 };
        var symbol = new KSymbol(build) { Hash = 12, FrameCount = 1 };
        build.Symbols.Add(symbol);
        build.SymbolNames[symbol.Hash] = "body";
        var frame = new KFrame(symbol)
        {
            Index = 2,
            Duration = 3,
            PivotWidth = 10,
            PivotHeight = 8,
            UV_X1 = 0,
            UV_Y1 = 0,
            UV_X2 = 0.5f,
            UV_Y2 = 0.5f
        };
        symbol.Frames.Add(frame);
        var anim = new KAnim();
        var bank = new KAnimBank(anim) { Name = "idle" };
        var animFrame = new KAnimFrame(bank) { X = 1, Y = 2, Width = 3, Height = 4 };
        var element = new KAnimElement(animFrame)
        {
            SymbolHash = 12,
            FrameNumber = 2,
            FolderHash = 13,
            Flags = 4,
            Red = 0.1f,
            Green = 0.2f,
            Blue = 0.3f,
            Alpha = 0.4f,
            M00 = 1,
            M10 = 2,
            M01 = 3,
            M11 = 4,
            M02 = 5,
            M12 = 6,
            Unused = 7
        };
        animFrame.Elements.Add(element);

        var inspector = new PreviewParameterInspector();
        Assert.Contains(inspector.Describe(build), item => item.Key == "Build 名称");
        Assert.Contains(inspector.Describe(symbol), item => item.Key == "Symbol 名称");
        Assert.Contains(inspector.Describe(frame, 100, 80), item => item.Key == "锚点");
        Assert.Contains(inspector.Describe(animFrame), item => item.Key == "元素数量");
        Assert.Contains(inspector.Describe(element), item => item.Key == "M12");
        Assert.Equal("无可用参数信息", inspector.Describe(null).Single().Key);
    }

    [Fact]
    public void AnimationSessionHandlesEmptyBanksResetAndPreferredFallback()
    {
        var anim = new KAnim();
        var empty = new KAnimBank(anim) { Name = "empty" };
        var populated = new KAnimBank(anim) { Name = "other" };
        var populatedFrame = new KAnimFrame(populated);
        populatedFrame.Elements.Add(new KAnimElement(populatedFrame));
        populated.Frames.Add(populatedFrame);
        anim.Banks.Add(empty);
        anim.Banks.Add(populated);
        var session = new PreviewAnimationSession();

        Assert.Equal(1, PreviewAnimationSession.GetPreferredBankIndex(anim.Banks));
        Assert.False(session.SelectFrame(1));
        Assert.False(session.EnsureCurrentFrame());
        Assert.Equal(-1, session.StepFrame(1));
        session.SelectBank(empty);
        Assert.Equal(-1, session.CurrentFrameIndex);
        Assert.False(session.EnsureCurrentFrame());
        session.SelectBank(populated);
        session.SelectElement(0);
        Assert.True(session.EnsureCurrentFrame());
        session.Reset();
        Assert.Null(session.CurrentBank);
        Assert.Equal(-1, session.SelectedElementIndex);
        Assert.Throws<ArgumentNullException>(() => session.SelectBank(null!));
        Assert.Equal(-1, PreviewAnimationSession.GetPreferredBankIndex(null));
    }

    [Fact]
    public void DiagnosticsEngineReportsStructuralIssuesAndFormatsReport()
    {
        var build = new KBuild { Version = 99, SymbolCount = 2, FrameCount = 3 };
        var symbol = new KSymbol(build) { Hash = 11, FrameCount = 2 };
        build.Symbols.Add(symbol);
        var buildFrame = new KFrame(symbol)
        {
            PivotWidth = 0,
            PivotHeight = 0,
            PivotX = float.NaN,
            UV_X1 = 0.8f,
            UV_Y1 = 0.8f,
            UV_X2 = 0.2f,
            UV_Y2 = 0.2f
        };
        symbol.Frames.Add(buildFrame);

        var anim = new KAnim { Version = 0, BankCount = 2, FrameCount = 3, ElementCount = 5 };
        var bank = new KAnimBank(anim) { Name = "idle", Rate = 0, FrameCount = 2 };
        var animFrame = new KAnimFrame(bank) { ElementCount = 2 };
        animFrame.Elements.Add(new KAnimElement(animFrame)
        {
            SymbolHash = 999,
            Red = 2,
            M00 = float.NaN
        });
        bank.Frames.Add(animFrame);
        anim.Banks.Add(bank);

        var package = new KAnimDataPackage(build, anim, 16, 16);
        var diagnostics = KAnimDiagnosticsEngine.Analyze(package);

        Assert.Contains(diagnostics, item => item.Code == "BUILD_VERSION");
        Assert.Contains(diagnostics, item => item.Code == "FRAME_PIVOT_NAN");
        Assert.Contains(diagnostics, item => item.Code == "ANIM_BANK_COUNT");
        Assert.Contains(diagnostics, item => item.Code == "BANK_RATE");
        Assert.Contains(diagnostics, item => item.Code == "ELEMENT_SYMBOL_MISSING");
        var report = KAnimDiagnosticsEngine.FormatReport(package, diagnostics);
        Assert.Contains("KAnim 诊断报告", report);
        Assert.Contains("ELEMENT_SYMBOL_MISSING", report);
    }
}
