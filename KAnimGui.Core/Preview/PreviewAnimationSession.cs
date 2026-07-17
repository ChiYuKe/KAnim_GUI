using KanimLib;

namespace KAnimGui.Core.Preview;

/// <summary>
/// UI-neutral animation selection state used by the previewer.
/// </summary>
public sealed class PreviewAnimationSession
{
    public KAnimBank? CurrentBank { get; private set; }
    public int CurrentFrameIndex { get; private set; } = -1;
    public int SelectedElementIndex { get; private set; } = -1;

    public KAnimFrame? CurrentFrame =>
        CurrentBank != null && CurrentFrameIndex >= 0 && CurrentFrameIndex < CurrentBank.Frames.Count
            ? CurrentBank.Frames[CurrentFrameIndex]
            : null;

    public void Reset()
    {
        CurrentBank = null;
        CurrentFrameIndex = -1;
        SelectedElementIndex = -1;
    }

    public void SelectBank(KAnimBank bank)
    {
        ArgumentNullException.ThrowIfNull(bank);
        CurrentBank = bank;
        CurrentFrameIndex = bank.Frames.Count > 0 ? 0 : -1;
        SelectedElementIndex = -1;
    }

    public bool SelectFrame(int frameIndex)
    {
        if (CurrentBank == null || CurrentBank.Frames.Count == 0)
        {
            return false;
        }

        CurrentFrameIndex = Math.Clamp(frameIndex, 0, CurrentBank.Frames.Count - 1);
        SelectedElementIndex = -1;
        return true;
    }

    public bool EnsureCurrentFrame()
    {
        if (CurrentBank == null || CurrentBank.Frames.Count == 0)
        {
            CurrentFrameIndex = -1;
            return false;
        }

        CurrentFrameIndex = Math.Clamp(CurrentFrameIndex, 0, CurrentBank.Frames.Count - 1);
        return true;
    }

    public int StepFrame(int delta)
    {
        if (CurrentBank == null || CurrentBank.Frames.Count == 0)
        {
            return -1;
        }

        CurrentFrameIndex = (CurrentFrameIndex + delta) % CurrentBank.Frames.Count;
        if (CurrentFrameIndex < 0)
        {
            CurrentFrameIndex += CurrentBank.Frames.Count;
        }

        SelectedElementIndex = -1;
        return CurrentFrameIndex;
    }

    public void SelectElement(int elementIndex)
    {
        SelectedElementIndex = elementIndex;
    }

    public static int GetPreferredBankIndex(IReadOnlyList<KAnimBank>? banks)
    {
        if (banks == null || banks.Count == 0)
        {
            return -1;
        }

        string[] preferredNames = ["working_loop", "on", "idle", "off", "working_pre"];
        foreach (string preferredName in preferredNames)
        {
            for (int index = 0; index < banks.Count; index++)
            {
                if (string.Equals(banks[index].Name, preferredName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }
        }

        int bestIndex = 0;
        int bestElementCount = -1;
        for (int index = 0; index < banks.Count; index++)
        {
            int elementCount = banks[index].Frames.Count > 0 ? banks[index].Frames[0].Elements.Count : 0;
            if (elementCount > bestElementCount)
            {
                bestIndex = index;
                bestElementCount = elementCount;
            }
        }

        return bestIndex;
    }
}
