using System.Windows.Controls;
using System.Windows.Threading;
using KanimLib;
using KAnimGui.Core.Preview;

namespace KAnimGui.Presentation.Preview;

/// <summary>
/// Coordinates animation selection, playback timing, and frame navigator controls.
/// </summary>
public sealed class KAnimPreviewPlaybackCoordinator : IDisposable
{
    private readonly DispatcherTimer timer;
    private readonly ComboBox animationComboBox;
    private readonly Button playPauseButton;
    private readonly Button previousFrameButton;
    private readonly Button nextFrameButton;
    private readonly Slider frameSlider;
    private readonly Slider playbackSpeedSlider;
    private readonly ListBox frameListBox;
    private readonly ListBox elementListBox;
    private readonly TextBlock frameStatusText;
    private readonly TextBlock playbackSpeedText;
    private readonly Func<KAnimPackage?> getData;
    private readonly Action renderCurrentFrame;
    private readonly Action<object> updateParameterInfo;
    private readonly Action clearCaches;
    private readonly PreviewPlaybackController playbackController;
    private readonly PreviewAnimationSession animationSession;
    private bool isUpdatingFrameSelection;
    private bool isInitializingAnimationSelection;
    private bool isPlaying;
    private double playbackSpeedMultiplier = 1.0;
    private DateTime lastPlaybackStatusUpdate = DateTime.MinValue;

    public KAnimPreviewPlaybackCoordinator(
        DispatcherTimer timer,
        ComboBox animationComboBox,
        Button playPauseButton,
        Button previousFrameButton,
        Button nextFrameButton,
        Slider frameSlider,
        Slider playbackSpeedSlider,
        ListBox frameListBox,
        ListBox elementListBox,
        TextBlock frameStatusText,
        TextBlock playbackSpeedText,
        Func<KAnimPackage?> getData,
        Action renderCurrentFrame,
        Action<object> updateParameterInfo,
        PreviewPlaybackController? playbackController = null,
        PreviewAnimationSession? animationSession = null,
        Action? clearCaches = null)
    {
        this.timer = timer ?? throw new ArgumentNullException(nameof(timer));
        this.animationComboBox = animationComboBox ?? throw new ArgumentNullException(nameof(animationComboBox));
        this.playPauseButton = playPauseButton ?? throw new ArgumentNullException(nameof(playPauseButton));
        this.previousFrameButton = previousFrameButton ?? throw new ArgumentNullException(nameof(previousFrameButton));
        this.nextFrameButton = nextFrameButton ?? throw new ArgumentNullException(nameof(nextFrameButton));
        this.frameSlider = frameSlider ?? throw new ArgumentNullException(nameof(frameSlider));
        this.playbackSpeedSlider = playbackSpeedSlider ?? throw new ArgumentNullException(nameof(playbackSpeedSlider));
        this.frameListBox = frameListBox ?? throw new ArgumentNullException(nameof(frameListBox));
        this.elementListBox = elementListBox ?? throw new ArgumentNullException(nameof(elementListBox));
        this.frameStatusText = frameStatusText ?? throw new ArgumentNullException(nameof(frameStatusText));
        this.playbackSpeedText = playbackSpeedText ?? throw new ArgumentNullException(nameof(playbackSpeedText));
        this.getData = getData ?? throw new ArgumentNullException(nameof(getData));
        this.renderCurrentFrame = renderCurrentFrame ?? throw new ArgumentNullException(nameof(renderCurrentFrame));
        this.updateParameterInfo = updateParameterInfo ?? throw new ArgumentNullException(nameof(updateParameterInfo));
        this.clearCaches = clearCaches ?? (() => { });
        this.playbackController = playbackController ?? new PreviewPlaybackController();
        this.animationSession = animationSession ?? new PreviewAnimationSession();
        this.timer.Tick += Timer_Tick;
    }

    public KAnimBank? CurrentBank => animationSession.CurrentBank;
    public int CurrentFrameIndex => animationSession.CurrentFrameIndex;
    public int SelectedElementIndex => animationSession.SelectedElementIndex;
    public KAnimFrame? CurrentFrame =>
        CurrentBank is { } bank && CurrentFrameIndex >= 0 && CurrentFrameIndex < bank.Frames.Count
            ? bank.Frames[CurrentFrameIndex]
            : null;

    public void Initialize()
    {
        Stop();
        animationSession.Reset();
        isInitializingAnimationSelection = true;
        try
        {
            animationComboBox.ItemsSource = getData()?.Anim?.Banks;
            animationComboBox.SelectedIndex = GetPreferredAnimationIndex();
        }
        finally
        {
            isInitializingAnimationSelection = false;
        }

        if (animationComboBox.SelectedItem is KAnimBank selectedBank)
        {
            SetCurrentBank(selectedBank);
        }
        else
        {
            frameStatusText.Text = "未加载动画";
            SetPlaybackControlsEnabled(false);
            UpdateFrameNavigator();
        }
    }

    public void SetCurrentBank(KAnimBank bank)
    {
        Stop();
        animationSession.SelectBank(bank);
        playbackController.Reset(bank.Frames.Count);
        SetPlaybackControlsEnabled(bank.Frames.Count > 0);
        UpdateFrameNavigator();
        UpdatePlaybackInterval();
        renderCurrentFrame();
    }

    public void SelectFrameAndElement(int frameIndex, int elementIndex)
    {
        animationSession.SelectFrame(frameIndex);
        animationSession.SelectElement(elementIndex);
    }

    public void SelectElement(int elementIndex)
    {
        animationSession.SelectElement(elementIndex);
    }

    public void OnAnimationSelectionChanged()
    {
        if (animationComboBox.SelectedItem is KAnimBank bank)
        {
            SetCurrentBank(bank);
            if (!isInitializingAnimationSelection && Properties.Default.PreviewAutoPlayAnimation)
            {
                Start();
            }
        }
    }

    /// <summary>
    /// Switches animations directly from the wheel while the pointer is over the animation selector.
    /// Keeping this here ensures the same selection, playback, and rendering path is used as a click.
    /// </summary>
    public void OnAnimationMouseWheel(int delta)
    {
        if (delta == 0 || animationComboBox.Items.Count < 2)
        {
            return;
        }

        int currentIndex = animationComboBox.SelectedIndex;
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        int nextIndex = delta > 0
            ? Math.Max(0, currentIndex - 1)
            : Math.Min(animationComboBox.Items.Count - 1, currentIndex + 1);

        if (nextIndex != animationComboBox.SelectedIndex)
        {
            animationComboBox.SelectedIndex = nextIndex;
        }
    }

    public void OnPlayPause()
    {
        if (CurrentBank is null || CurrentBank.Frames.Count == 0)
        {
            return;
        }

        if (isPlaying)
        {
            Stop();
        }
        else
        {
            Start();
        }
    }

    public void OnPreviousFrame()
    {
        Stop();
        Step(-1);
    }

    public void OnNextFrame()
    {
        Stop();
        Step(1);
    }

    public void OnFrameSliderChanged(double value)
    {
        if (!isUpdatingFrameSelection && CurrentBank is not null)
        {
            JumpTo((int)Math.Round(value));
        }
    }

    public void OnFrameListSelectionChanged(object? selectedItem)
    {
        if (!isUpdatingFrameSelection && selectedItem is FrameListItem item)
        {
            JumpTo(item.Index);
        }
    }

    public void OnElementListSelectionChanged(object? selectedItem)
    {
        if (isUpdatingFrameSelection)
        {
            return;
        }

        if (selectedItem is ElementListItem item)
        {
            SelectElement(item.Index);
            updateParameterInfo(item.Element);
        }
        else
        {
            SelectElement(-1);
        }

        renderCurrentFrame();
    }

    public void OnPlaybackSpeedChanged(double value)
    {
        playbackSpeedMultiplier = Math.Clamp(value, 0.1, 2.0);
        playbackSpeedText.Text = $"速度 {playbackSpeedMultiplier:0.00}x";
        if (CurrentBank is not null)
        {
            UpdatePlaybackInterval();
            frameStatusText.Text = BuildFrameStatusText();
        }
    }

    public void Stop()
    {
        isPlaying = false;
        playbackController.SetPlaying(false);
        timer.Stop();
        clearCaches();
        playPauseButton.Content = new MaterialDesignThemes.Wpf.PackIcon
        {
            Kind = MaterialDesignThemes.Wpf.PackIconKind.Play
        };
    }

    public void Start()
    {
        if (CurrentBank is null || CurrentBank.Frames.Count == 0)
        {
            return;
        }

        isPlaying = true;
        playbackController.SetPlaying(true);
        playPauseButton.Content = new MaterialDesignThemes.Wpf.PackIcon
        {
            Kind = MaterialDesignThemes.Wpf.PackIconKind.Pause
        };
        UpdatePlaybackInterval();
        timer.Start();
    }

    public string BuildFrameStatusText()
    {
        return CurrentBank is null
            ? "未加载动画"
            : $"{CurrentBank.Name}  {CurrentFrameIndex + 1}/{CurrentBank.Frames.Count}  {CurrentBank.Rate:0.##} fps · {playbackSpeedMultiplier:0.##}x";
    }

    public void RenderedFrame(KAnimFrame frame)
    {
        SyncFrameSelection(frame);
        if (isPlaying)
        {
            var now = DateTime.UtcNow;
            if ((now - lastPlaybackStatusUpdate).TotalMilliseconds >= 250)
            {
                frameStatusText.Text = BuildFrameStatusText();
                lastPlaybackStatusUpdate = now;
            }
        }
        else
        {
            frameStatusText.Text = BuildFrameStatusText();
            updateParameterInfo(frame);
        }
    }

    private void Timer_Tick(object? sender, EventArgs e) => Step(1);

    public void Step(int delta)
    {
        if (CurrentBank is null || CurrentBank.Frames.Count == 0)
        {
            return;
        }

        animationSession.SelectFrame(playbackController.Step(delta));
        renderCurrentFrame();
    }

    public void JumpTo(int frameIndex)
    {
        if (CurrentBank is null || CurrentBank.Frames.Count == 0)
        {
            return;
        }

        Stop();
        animationSession.SelectFrame(playbackController.JumpTo(frameIndex));
        renderCurrentFrame();
    }

    private void SetPlaybackControlsEnabled(bool isEnabled)
    {
        playPauseButton.IsEnabled = isEnabled;
        previousFrameButton.IsEnabled = isEnabled;
        nextFrameButton.IsEnabled = isEnabled;
        frameSlider.IsEnabled = isEnabled;
        playbackSpeedSlider.IsEnabled = isEnabled;
        frameListBox.IsEnabled = isEnabled;
        elementListBox.IsEnabled = isEnabled;
    }

    private void UpdatePlaybackInterval()
    {
        var rate = CurrentBank?.Rate > 0 ? CurrentBank.Rate : 30;
        timer.Interval = PreviewPlaybackController.CalculateInterval(rate, playbackSpeedMultiplier);
    }

    private int GetPreferredAnimationIndex()
    {
        var banks = getData()?.Anim?.Banks;
        if (banks is null || banks.Count == 0)
        {
            return -1;
        }

        var preferredNames = new[] { "working_loop", "on", "idle", "off", "working_pre" };
        foreach (var preferredName in preferredNames)
        {
            for (int i = 0; i < banks.Count; i++)
            {
                if (string.Equals(banks[i].Name, preferredName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        int bestIndex = 0;
        int bestElementCount = -1;
        for (int i = 0; i < banks.Count; i++)
        {
            int elementCount = banks[i].Frames.Count > 0 ? banks[i].Frames[0].Elements.Count : 0;
            if (elementCount > bestElementCount)
            {
                bestIndex = i;
                bestElementCount = elementCount;
            }
        }

        return bestIndex;
    }

    private void UpdateFrameNavigator()
    {
        isUpdatingFrameSelection = true;
        try
        {
            if (CurrentBank is null || CurrentBank.Frames.Count == 0)
            {
                frameSlider.Maximum = 0;
                frameSlider.Value = 0;
                frameListBox.ItemsSource = null;
                elementListBox.ItemsSource = null;
                return;
            }

            frameSlider.Maximum = CurrentBank.Frames.Count - 1;
            frameSlider.Value = CurrentFrameIndex;
            frameListBox.ItemsSource = CurrentBank.Frames
                .Select((frame, index) => new FrameListItem(index, $"{index}: {frame.Elements.Count}"))
                .ToList();
        }
        finally
        {
            isUpdatingFrameSelection = false;
        }
    }

    private void SyncFrameSelection(KAnimFrame frame)
    {
        isUpdatingFrameSelection = true;
        try
        {
            frameSlider.Value = CurrentFrameIndex;
            frameListBox.SelectedIndex = CurrentFrameIndex;
            elementListBox.ItemsSource = frame.Elements
                .Select((element, index) => new ElementListItem(
                    index,
                    element,
                    $"{index}: {GetElementDisplayName(element)}"))
                .ToList();
            elementListBox.SelectedIndex = SelectedElementIndex >= 0 && SelectedElementIndex < frame.Elements.Count
                ? SelectedElementIndex
                : -1;
        }
        finally
        {
            isUpdatingFrameSelection = false;
        }
    }

    private string GetElementDisplayName(KAnimElement element)
    {
        var symbolName = getData()?.Build?.GetSymbol(element.SymbolHash)?.Name ?? element.SymbolHash.ToString();
        return $"{symbolName} f{element.FrameNumber}";
    }

    public void Dispose()
    {
        Stop();
        timer.Tick -= Timer_Tick;
    }

    public sealed record FrameListItem(int Index, string Title);
    public sealed record ElementListItem(int Index, KAnimElement Element, string Title);
}
