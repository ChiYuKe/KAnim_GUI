using KAnimGui.Core.Kanim;
using KanimLib;

namespace KAnimGui.Tests;

public sealed class KAnimTreeModelTests
{
    [Fact]
    public void Build_FiltersSymbolsAndKeepsMatchingAnimationBank()
    {
        (KBuild build, KAnim anim) = CreateData();
        var builder = new KAnimTreeModelBuilder();

        var nodes = builder.Build(build, anim, "body");

        Assert.Equal(2, nodes.Count);
        var buildNode = Assert.Single(nodes, node => node.Kind == KAnimTreeNodeKind.Build);
        Assert.Equal("body", Assert.Single(buildNode.Children).Value is KSymbol symbol ? symbol.Name : string.Empty);
        var animNode = Assert.Single(nodes, node => node.Kind == KAnimTreeNodeKind.Anim);
        Assert.Single(animNode.Children);
    }

    [Fact]
    public void ExpandedBank_FiltersElementsByResolvedSymbolName()
    {
        (KBuild build, KAnim anim) = CreateData();
        var builder = new KAnimTreeModelBuilder();
        var bank = Assert.Single(anim.Banks);

        var nodes = builder.BuildExpandedBank(bank, build, "body");

        var frame = Assert.Single(nodes);
        var element = Assert.Single(frame.Children);
        Assert.Equal(KAnimTreeNodeKind.Element, element.Kind);
        Assert.Equal(0, element.ElementIndex);
    }

    private static (KBuild Build, KAnim Anim) CreateData()
    {
        var build = new KBuild { SymbolCount = 2, FrameCount = 2 };
        var body = new KSymbol(build) { Hash = "body".KHash(), Path = "body".KHash(), FrameCount = 1 };
        body.Frames.Add(new KFrame(body) { Index = 0, Duration = 1, PivotWidth = 2, PivotHeight = 2 });
        var head = new KSymbol(build) { Hash = "head".KHash(), Path = "head".KHash(), FrameCount = 1 };
        head.Frames.Add(new KFrame(head) { Index = 0, Duration = 1, PivotWidth = 2, PivotHeight = 2 });
        build.Symbols.Add(body);
        build.Symbols.Add(head);
        build.SymbolNames[body.Hash] = "body";
        build.SymbolNames[head.Hash] = "head";

        var anim = new KAnim { BankCount = 1, FrameCount = 1, ElementCount = 2 };
        var bank = new KAnimBank(anim) { Name = "idle", FrameCount = 1 };
        var frame = new KAnimFrame(bank) { ElementCount = 2 };
        frame.Elements.Add(new KAnimElement(frame) { SymbolHash = body.Hash });
        frame.Elements.Add(new KAnimElement(frame) { SymbolHash = head.Hash });
        bank.Frames.Add(frame);
        anim.Banks.Add(bank);
        return (build, anim);
    }
}
