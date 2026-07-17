namespace KAnimGui.Core.Preview;

public sealed class PreviewPlaybackController
{
    public int FrameCount { get; private set; }

    public int CurrentFrameIndex { get; private set; } = -1;

    public bool IsPlaying { get; private set; }

    public void Reset(int frameCount)
    {
        FrameCount = Math.Max(0, frameCount);
        CurrentFrameIndex = FrameCount > 0 ? 0 : -1;
        IsPlaying = false;
    }

    public void SetPlaying(bool isPlaying)
    {
        IsPlaying = isPlaying && FrameCount > 0;
    }

    public int Step(int delta)
    {
        if (FrameCount == 0)
        {
            CurrentFrameIndex = -1;
            return CurrentFrameIndex;
        }

        int index = CurrentFrameIndex < 0 ? 0 : CurrentFrameIndex;
        CurrentFrameIndex = (index + delta % FrameCount + FrameCount) % FrameCount;
        return CurrentFrameIndex;
    }

    public int JumpTo(int frameIndex)
    {
        if (FrameCount == 0)
        {
            CurrentFrameIndex = -1;
            return CurrentFrameIndex;
        }

        CurrentFrameIndex = Math.Clamp(frameIndex, 0, FrameCount - 1);
        return CurrentFrameIndex;
    }

    public static TimeSpan CalculateInterval(double rate, double speedMultiplier)
    {
        double safeRate = rate > 0 ? rate : 30;
        double safeSpeed = Math.Clamp(speedMultiplier, 0.1, 2.0);
        return TimeSpan.FromMilliseconds(1000.0 / (safeRate * safeSpeed));
    }
}
