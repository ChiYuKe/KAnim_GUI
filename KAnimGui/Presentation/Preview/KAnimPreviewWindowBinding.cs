using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace KAnimGui.Presentation.Preview;

/// <summary>
/// Keeps WPF event hookup out of the preview window's lifecycle code.
/// </summary>
public sealed class KAnimPreviewWindowBinding
{
    private readonly FrameworkElement root;

    public KAnimPreviewWindowBinding(FrameworkElement root)
    {
        this.root = root ?? throw new ArgumentNullException(nameof(root));
    }

    public void Attach(
        KAnimPreviewTreeController tree,
        KAnimPreviewPlaybackCoordinator playback,
        KAnimPreviewCommandController commands,
        KAnimPreviewDropController drop,
        Action setDarkBackground,
        Action setLightBackground,
        Action close,
        Action renderCurrentFrame)
    {
        var search = Find<TextBox>("TreeSearchBox");
        var treeView = Find<TreeView>("BuildTreeView");
        var dropCard = Find<Card>("DropCard");
        var previewSurface = Find<Border>("PreviewSurface");
        var viewport = Find<AnimationViewport>("PreviewViewport");
        var frameList = Find<ListBox>("FrameListBox");
        var elementList = Find<ListBox>("ElementListBox");
        var animation = Find<ComboBox>("AnimationComboBox");
        var frameSlider = Find<Slider>("FrameSlider");
        var speedSlider = Find<Slider>("PlaybackSpeedSlider");
        var previous = Find<Button>("PreviousFrameButton");
        var playPause = Find<Button>("PlayPauseButton");
        var next = Find<Button>("NextFrameButton");
        var origin = Find<CheckBox>("ShowOriginCheckBox");
        var bounds = Find<CheckBox>("ShowBoundsCheckBox");
        var highlight = Find<CheckBox>("HighlightElementCheckBox");

        search.TextChanged += (_, _) => tree.SetSearchText(search.Text);
        treeView.SelectedItemChanged += (_, _) => tree.HandleSelectedItemChanged();
        treeView.AddHandler(TreeViewItem.PreviewMouseRightButtonDownEvent,
            new MouseButtonEventHandler(SelectTreeItemOnRightClick));

        dropCard.DragEnter += (_, e) => drop.DragEnter(e);
        dropCard.DragLeave += (_, _) => drop.DragLeave();
        dropCard.Drop += (_, e) => drop.Drop(e);
        dropCard.MouseDown += (_, e) =>
        {
            if (e.ClickCount >= 2)
            {
                drop.DoubleClick();
            }
        };
        dropCard.MouseEnter += (_, _) => drop.MouseEnter();
        previewSurface.DragEnter += (_, e) => drop.DragEnter(e);
        previewSurface.DragLeave += (_, _) => drop.DragLeave();
        previewSurface.Drop += (_, e) => drop.Drop(e);

        frameList.SelectionChanged += (_, _) => playback.OnFrameListSelectionChanged(frameList.SelectedItem);
        elementList.SelectionChanged += (_, _) => playback.OnElementListSelectionChanged(elementList.SelectedItem);
        animation.SelectionChanged += (_, _) => playback.OnAnimationSelectionChanged();
        animation.PreviewMouseWheel += (_, e) =>
        {
            playback.OnAnimationMouseWheel(e.Delta);
            e.Handled = true;
        };
        root.PreviewKeyDown += (_, e) =>
        {
            if (!IsResetViewKey(e) || e.IsRepeat || IsTextInputFocused())
            {
                return;
            }

            viewport.ResetTransform();
            viewport.Focus();
            e.Handled = true;
        };
        previous.Click += (_, _) => playback.OnPreviousFrame();
        playPause.Click += (_, _) => playback.OnPlayPause();
        next.Click += (_, _) => playback.OnNextFrame();
        frameSlider.ValueChanged += (_, e) => playback.OnFrameSliderChanged(e.NewValue);
        speedSlider.ValueChanged += (_, e) => playback.OnPlaybackSpeedChanged(e.NewValue);
        origin.Checked += (_, _) => renderCurrentFrame();
        origin.Unchecked += (_, _) => renderCurrentFrame();
        bounds.Checked += (_, _) => renderCurrentFrame();
        bounds.Unchecked += (_, _) => renderCurrentFrame();
        highlight.Checked += (_, _) => renderCurrentFrame();
        highlight.Unchecked += (_, _) => renderCurrentFrame();

        Find<MenuItem>("MenuOpen").Click += (_, _) =>
        {
            if (commands.HasAnyFile)
            {
                commands.Reset();
            }
            else
            {
                commands.OpenFilesDialog();
            }
        };
        Find<MenuItem>("MenuExportPng").Click += (_, _) => commands.ExportTexture();
        Find<MenuItem>("MenuExportGif").Click += async (_, _) => await commands.ExportAnimationGifAsync();
        Find<MenuItem>("MenuExit").Click += (_, _) => close();
        Find<MenuItem>("MenuBackgroundBlack").Click += (_, _) => setDarkBackground();
        Find<MenuItem>("MenuBackgroundWhite").Click += (_, _) => setLightBackground();
        Find<MenuItem>("MenuPlayPause").Click += (_, _) => playback.OnPlayPause();
        Find<MenuItem>("MenuPreviousFrame").Click += (_, _) => playback.OnPreviousFrame();
        Find<MenuItem>("MenuNextFrame").Click += (_, _) => playback.OnNextFrame();
        Find<MenuItem>("MenuDiagnosePackage").Click += (_, _) => commands.Diagnose();
        if (treeView.Resources["TreeViewItemContextMenu"] is ContextMenu contextMenu)
        {
            ((MenuItem)contextMenu.Items[0]).Click += (_, _) => commands.ExportSelectedImage();
            ((MenuItem)contextMenu.Items[1]).Click += (_, _) => commands.ReplaceSelectedImage();
        }
    }

    private T Find<T>(string name) where T : class => (root.FindName(name) as T)
        ?? throw new InvalidOperationException($"Preview control '{name}' was not found.");

    private static void SelectTreeItemOnRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeView tree && e.OriginalSource is DependencyObject source)
        {
            var item = ItemsControl.ContainerFromElement(tree, source) as TreeViewItem;
            if (item != null)
            {
                item.IsSelected = true;
            }
        }
    }

    private static bool IsResetViewKey(KeyEventArgs e) =>
        e.Key == Key.H ||
        (e.Key == Key.ImeProcessed && e.ImeProcessedKey == Key.H);

    private static bool IsTextInputFocused() =>
        Keyboard.FocusedElement is TextBoxBase or PasswordBox or RichTextBox;
}
