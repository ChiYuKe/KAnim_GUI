using KAnimGui.Core.Preview;
using KanimLib;

namespace KAnimGui.Tests;

public sealed class PreviewAnimationSessionTests
{
    [Fact]
    public void Session_SelectsPreferredBankAndWrapsFrames()
    {
        var anim = new KAnim();
        var bank = new KAnimBank(anim) { Name = "idle", FrameCount = 2 };
        bank.Frames.Add(new KAnimFrame(bank));
        bank.Frames.Add(new KAnimFrame(bank));
        anim.Banks.Add(bank);
        var session = new PreviewAnimationSession();

        Assert.Equal(0, PreviewAnimationSession.GetPreferredBankIndex(anim.Banks));
        session.SelectBank(bank);
        Assert.Equal(1, session.StepFrame(-1));
        Assert.Equal(0, session.StepFrame(1));
        Assert.True(session.SelectFrame(99));
        Assert.Equal(1, session.CurrentFrameIndex);
    }
}
