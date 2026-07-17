using System.Drawing;
using KanimLib;

namespace KAnimGui.Tests;

public sealed class KAnimBinaryCodecTests
{
    [Fact]
    public void BuildRoundTrip_PreservesFramesAndHashes()
    {
        var build = new KBuild
        {
            Name = "hero",
            Version = KBuild.CURRENT_BUILD_VERSION,
            SymbolCount = 1,
            FrameCount = 1
        };
        var symbol = new KSymbol(build)
        {
            Hash = "body".KHash(),
            Path = "body".KHash(),
            Color = Color.FromArgb(255, 10, 20, 30),
            FrameCount = 1
        };
        var frame = new KFrame(symbol)
        {
            Index = 0,
            Duration = 2,
            PivotWidth = 64,
            PivotHeight = 32,
            UV_X1 = 0.1f,
            UV_Y1 = 0.2f,
            UV_X2 = 0.3f,
            UV_Y2 = 0.4f
        };
        symbol.Frames.Add(frame);
        build.Symbols.Add(symbol);
        build.SymbolNames[symbol.Hash] = "body";

        using var stream = new MemoryStream();
        Assert.True(KAnimBinaryCodec.WriteBuild(stream, build));
        stream.Position = 0;

        KBuild loaded = KAnimBinaryCodec.ReadBuild(stream);

        Assert.Equal("hero", loaded.Name);
        Assert.Single(loaded.Symbols);
        Assert.Equal("body", loaded.SymbolNames["body".KHash()]);
        Assert.Equal(2, loaded.Symbols[0].Frames[0].Duration);
        Assert.Equal(0.3f, loaded.Symbols[0].Frames[0].UV_X2);
    }

    [Fact]
    public void AnimRoundTrip_PreservesElementTransform()
    {
        var anim = new KAnim
        {
            Version = 5,
            ElementCount = 1,
            FrameCount = 1,
            BankCount = 1,
            MaxVisSymbols = 4
        };
        var bank = new KAnimBank(anim)
        {
            Name = "idle",
            Hash = "idle".KHash(),
            Rate = 12,
            FrameCount = 1
        };
        var frame = new KAnimFrame(bank)
        {
            X = 1,
            Y = 2,
            Width = 3,
            Height = 4,
            ElementCount = 1
        };
        var element = new KAnimElement(frame)
        {
            SymbolHash = "body".KHash(),
            FrameNumber = 2,
            Alpha = 0.75f,
            M00 = 0.5f,
            M11 = 0.25f,
            M02 = 10,
            M12 = -4
        };
        frame.Elements.Add(element);
        bank.Frames.Add(frame);
        anim.Banks.Add(bank);
        anim.SymbolNames[element.SymbolHash] = "body";

        using var stream = new MemoryStream();
        Assert.True(KAnimBinaryCodec.WriteAnim(stream, anim));
        stream.Position = 0;

        KAnim loaded = KAnimBinaryCodec.ReadAnim(stream);

        var loadedElement = loaded.Banks[0].Frames[0].Elements[0];
        Assert.Equal(2, loadedElement.FrameNumber);
        Assert.Equal(0.75f, loadedElement.Alpha);
        Assert.Equal(0.5f, loadedElement.M00);
        Assert.Equal(-4, loadedElement.M12);
    }
}
