using KAnimGui.Core.Preview;

namespace KAnimGui.Tests;

public sealed class PreviewPlaybackControllerTests
{
    [Fact]
    public void Controller_WrapsAndClampsFrames()
    {
        var controller = new PreviewPlaybackController();
        controller.Reset(3);

        Assert.Equal(0, controller.CurrentFrameIndex);
        Assert.Equal(2, controller.Step(-1));
        Assert.Equal(0, controller.Step(4));
        Assert.Equal(2, controller.JumpTo(99));
        Assert.Equal(0, controller.JumpTo(-3));
    }

    [Fact]
    public void Controller_HandlesEmptyAnimationAndSpeedBounds()
    {
        var controller = new PreviewPlaybackController();
        controller.Reset(0);

        Assert.Equal(-1, controller.Step(1));
        Assert.False(controller.IsPlaying);
        Assert.Equal(TimeSpan.FromMilliseconds(1000.0 / 3), PreviewPlaybackController.CalculateInterval(0, 0.1));
        Assert.Equal(TimeSpan.FromMilliseconds(1000.0 / 60), PreviewPlaybackController.CalculateInterval(30, 2));
    }
}
