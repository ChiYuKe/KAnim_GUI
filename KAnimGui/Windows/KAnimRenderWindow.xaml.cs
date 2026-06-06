using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KanimLib;
using MaterialDesignThemes.Wpf;
using Brushes = System.Windows.Media.Brushes;
using Brush = System.Windows.Media.Brush;
using System.Collections.ObjectModel;
using KAnimGui.KAnimCore;

namespace KAnimGui.Windows
{
    public partial class KAnimRenderWindow : Window
    {
        // 当前加载的数据包，包含纹理、build、anim
        private KAnimPackage? data;

        // 当前打开的文件路径（纹理、动画、构建文件）
        private string? currentTextureFile;
        private string? currentAnimFile;
        private string? currentBuildFile;

        // 用于绘制文本的字体和大小
        private Typeface _typeface = new Typeface("Microsoft YaHei UI");
        private double _fontSize = 14;
        private const int AnimationCanvasSize = 768;
        private const int MaxCachedAnimationFrames = 8;
        private const int MaxCachedElementImages = 256;
        private readonly DispatcherTimer playbackTimer = new DispatcherTimer(DispatcherPriority.Render);
        private KAnimBank? currentBank;
        private int currentFrameIndex;
        private bool isPlaying;
        private bool isUpdatingFrameSelection;
        private bool isInitializingAnimationSelection;
        private double playbackSpeedMultiplier = 1.0;
        private DateTime lastPlaybackStatusUpdate = DateTime.MinValue;
        private readonly Dictionary<KAnimBank, List<BitmapSource?>> animationFrameCache = new();
        private readonly Queue<(KAnimBank Bank, int FrameIndex)> animationFrameCacheOrder = new();
        private readonly Dictionary<string, BitmapSource> elementImageCache = new();
        private readonly Queue<string> elementImageCacheOrder = new();
        private string treeSearchText = string.Empty;
        private int selectedElementIndex = -1;
        private bool isPanningPreview;
        private System.Windows.Point lastPreviewPanPoint;
        private double previewZoom = 1.0;
        private static readonly System.Windows.Media.Color DarkPreviewBackground = System.Windows.Media.Color.FromRgb(72, 72, 72);
        private static readonly System.Windows.Media.Color DarkPreviewMinorLine = System.Windows.Media.Color.FromArgb(80, 255, 255, 255);
        private static readonly System.Windows.Media.Color DarkPreviewMajorLine = System.Windows.Media.Color.FromArgb(170, 255, 255, 255);
        private static readonly System.Windows.Media.Color LightPreviewBackground = System.Windows.Media.Color.FromRgb(232, 232, 232);
        private static readonly System.Windows.Media.Color LightPreviewMinorLine = System.Windows.Media.Color.FromArgb(90, 130, 130, 130);
        private static readonly System.Windows.Media.Color LightPreviewMajorLine = System.Windows.Media.Color.FromArgb(170, 120, 80, 98);

        public class KeyValueItem
        {
            public string Key { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
        }

        public sealed class FrameListItem
        {
            public int Index { get; init; }
            public string Title { get; init; } = string.Empty;
        }

        public sealed class ElementListItem
        {
            public int Index { get; init; }
            public KAnimElement Element { get; init; } = null!;
            public string Title { get; init; } = string.Empty;
        }

        public KAnimRenderWindow()
        {
            InitializeComponent();
            playbackTimer.Tick += PlaybackTimer_Tick;
        }

        private static DrawingBrush CreatePreviewGridBrush(
            System.Windows.Media.Color background,
            System.Windows.Media.Color minorLine,
            System.Windows.Media.Color majorLine)
        {
            var group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(
                new SolidColorBrush(background),
                null,
                new RectangleGeometry(new Rect(0, 0, 96, 96))));

            group.Children.Add(new GeometryDrawing(
                null,
                new System.Windows.Media.Pen(new SolidColorBrush(minorLine), 1),
                new GeometryGroup
                {
                    Children =
                    {
                        new LineGeometry(new System.Windows.Point(48, 0), new System.Windows.Point(48, 96)),
                        new LineGeometry(new System.Windows.Point(0, 48), new System.Windows.Point(96, 48))
                    }
                }));

            group.Children.Add(new GeometryDrawing(
                null,
                new System.Windows.Media.Pen(new SolidColorBrush(majorLine), 1.4),
                new GeometryGroup
                {
                    Children =
                    {
                        new LineGeometry(new System.Windows.Point(0, 0), new System.Windows.Point(96, 0)),
                        new LineGeometry(new System.Windows.Point(0, 0), new System.Windows.Point(0, 96))
                    }
                }));

            return new DrawingBrush(group)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 96, 96),
                ViewportUnits = BrushMappingMode.Absolute
            };
        }

        private void SetPreviewBackground(bool dark)
        {
            PreviewSurface.Background = dark
                ? CreatePreviewGridBrush(DarkPreviewBackground, DarkPreviewMinorLine, DarkPreviewMajorLine)
                : CreatePreviewGridBrush(LightPreviewBackground, LightPreviewMinorLine, LightPreviewMajorLine);
        }

        private void PreviewBackgroundBlack_Click(object sender, RoutedEventArgs e)
        {
            SetPreviewBackground(true);
        }

        private void PreviewBackgroundWhite_Click(object sender, RoutedEventArgs e)
        {
            SetPreviewBackground(false);
        }

        /// <summary>
        /// 鼠标拖动文件进入拖放区域时触发
        /// 设置拖放效果和视觉反馈
        /// </summary>
        private void Card_DragEnter(object sender, DragEventArgs e)
        {
            // 判断是否是文件拖入
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy; // 允许复制操作

                if (sender is MaterialDesignThemes.Wpf.Card card)
                {
                    // 更改背景颜色提示用户
                    card.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 103, 80, 164));
                    // 更改图标和提示文本
                    UploadIcon.Kind = PackIconKind.CloudDownload;
                    HintText.Text = "松开以导入文件";
                }
            }
            else
            {
                e.Effects = DragDropEffects.None; // 不允许拖放
            }
        }

        /// <summary>
        /// 鼠标拖动文件离开拖放区域时触发
        /// 恢复默认视觉样式
        /// </summary>
        private void Card_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is MaterialDesignThemes.Wpf.Card card)
            {
                // 恢复默认背景
                card.Background = (Brush)FindResource("PanelBackgroundBrush");
                // 恢复默认图标和提示
                UploadIcon.Kind = PackIconKind.FileUpload;
                HintText.Text = "拖放 .png、_anim、_build 文件到此处";
            }
        }

        /// <summary>
        /// 文件拖放到区域时触发
        /// 解析文件类型，保存路径，并尝试加载数据
        /// </summary>
        private void Card_Drop(object sender, DragEventArgs e)
        {
            if (sender is MaterialDesignThemes.Wpf.Card card)
            {
                card.Background = (Brush)FindResource("PanelBackgroundBrush");
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                // 遍历拖入文件，区分文件类型
                foreach (var file in files)
                {
                    var filename = Path.GetFileName(file).ToLowerInvariant();

                    if (filename.EndsWith(".png"))
                        currentTextureFile = file;
                    else if (filename.EndsWith("_anim.bytes"))
                        currentAnimFile = file;
                    else if (filename.EndsWith("_build.bytes"))
                        currentBuildFile = file;
                }

                // 准备显示文件名和对应图标列表
                var displayFiles = new List<(string, PackIconKind)>();

                if (!string.IsNullOrEmpty(currentTextureFile))
                    displayFiles.Add((Path.GetFileName(currentTextureFile), PackIconKind.FileImageOutline));
                if (!string.IsNullOrEmpty(currentAnimFile))
                    displayFiles.Add((Path.GetFileName(currentAnimFile), PackIconKind.FileDocumentOutline));
                if (!string.IsNullOrEmpty(currentBuildFile))
                    displayFiles.Add((Path.GetFileName(currentBuildFile), PackIconKind.FileDocumentOutline));

                ShowFileList(displayFiles);

                // 当三种文件都准备好时，调用加载逻辑
                if (!string.IsNullOrEmpty(currentTextureFile) &&
                    !string.IsNullOrEmpty(currentAnimFile) &&
                    !string.IsNullOrEmpty(currentBuildFile))
                {
                    OpenFiles(currentTextureFile, currentBuildFile, currentAnimFile);
                }
                else
                {
                    // 这里可以选择弹窗提示缺少文件，或者保持静默
                    // MessageBox.Show("请拖入 .png、_anim.bytes 和 _build.bytes 文件，当前缺少文件", "缺少文件", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }



        /// <summary>
        /// 显示当前加载的文件列表（文件名+图标）
        /// </summary>
        private void ShowFileList(List<(string fileName, PackIconKind iconKind)> files)
        {
            ContentPanel.Children.Clear();

            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };

            foreach (var (fileName, iconKind) in files)
            {
                var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var icon = new PackIcon
                {
                    Kind = iconKind,
                    Width = 20,
                    Height = 20,
                    Foreground = Brushes.Gray,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 6, 0)
                };
                Grid.SetColumn(icon, 0);

                var text = new TextBlock
                {
                    Text = fileName,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 14,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(text, 1);

                grid.Children.Add(icon);
                grid.Children.Add(text);
                stack.Children.Add(grid);
            }

            ContentPanel.Children.Add(stack);
        }



        /// <summary>
        /// 根据传入路径加载图片和数据文件
        /// </summary>
        public void OpenFiles(string textureFile, string buildFile, string animFile)
        {
            currentTextureFile = textureFile;
            currentBuildFile = buildFile;
            currentAnimFile = animFile;
            ShowFileList(new List<(string, PackIconKind)>
            {
                (Path.GetFileName(currentTextureFile), PackIconKind.FileImageOutline),
                (Path.GetFileName(currentAnimFile), PackIconKind.FileDocumentOutline),
                (Path.GetFileName(currentBuildFile), PackIconKind.FileDocumentOutline)
            });

            var package = KAnimDecoder.LoadPackage(textureFile, buildFile, animFile);
            OpenData(package.Texture, package.Build, package.Anim);
        }

        public void OpenFilesAndPlay(string textureFile, string buildFile, string animFile)
        {
            OpenFiles(textureFile, buildFile, animFile);
            StartPlayback();
            Activate();
        }

        public async Task OpenFilesAndPlayAsync(string textureFile, string buildFile, string animFile)
        {
            currentTextureFile = textureFile;
            currentBuildFile = buildFile;
            currentAnimFile = animFile;
            ShowFileList(new List<(string, PackIconKind)>
            {
                (Path.GetFileName(currentTextureFile), PackIconKind.FileImageOutline),
                (Path.GetFileName(currentAnimFile), PackIconKind.FileDocumentOutline),
                (Path.GetFileName(currentBuildFile), PackIconKind.FileDocumentOutline)
            });

            StopPlayback();
            FrameStatusText.Text = "正在加载预览...";
            var package = await Task.Run(() => KAnimDecoder.LoadPackage(textureFile, buildFile, animFile));
            OpenData(package.Texture, package.Build, package.Anim);
            StartPlayback();
            Activate();
        }

        /// <summary>
        /// 更新界面显示：保存数据包，更新预览和树视图
        /// </summary>
        private void OpenData(BitmapImage? texture, KBuild? build, KAnim? anim)
        {
            data = new KAnimPackage
            {
                Texture = texture,
                Build = build,
                Anim = anim
            };

            ClearRenderCaches();
            UpdateTextureView(data.Texture);
            UpdateBuildTree(data);
            InitializeAnimationPlayback();
        }



      

        /// <summary>
        /// 树节点选中事件，根据不同类型高亮显示对应的区域
        /// </summary>
        private void BuildTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selectedItem = BuildTreeView.SelectedItem as TreeViewItem;
            if (selectedItem == null) return;
            if (data == null) return;

            if (selectedItem.Tag is TreeNodeTag nodeTag)
            {
                SyncAnimationSelectionFromTree(nodeTag);
            }

            var selectedObj = GetTreeItemValue(selectedItem);

            List<Rectangle> frames = new List<Rectangle>();
            List<PointF> pivots = new List<PointF>();
            var showTextureView = true;

            switch (selectedObj)
            {
                case KBuild build:
                case KAnim anim:
                    // 不绘制任何选中框
                    break;

                case KAnimBank:
                    showTextureView = false;
                    break;

                case KSymbol symbol:
                    if (data.Texture != null)
                    {
                        // 绘制 symbol 里的所有 frame 区域和锚点
                        foreach (var frame in symbol.Frames)
                        {
                            frames.Add(frame.GetTextureRectangle(data.Texture.PixelWidth, data.Texture.PixelHeight));
                            pivots.Add(frame.GetPivotPoint(data.Texture.PixelWidth, data.Texture.PixelHeight));
                        }
                    }
                    break;

                case KFrame frame:
                    if (data.Texture != null)
                    {
                        // 绘制单个 frame 区域和锚点
                        frames.Add(frame.GetTextureRectangle(data.Texture.PixelWidth, data.Texture.PixelHeight));
                        pivots.Add(frame.GetPivotPoint(data.Texture.PixelWidth, data.Texture.PixelHeight));
                    }
                    break;

                case KAnimFrame animFrame:
                    showTextureView = false;
                    if (data.Texture != null && data.Build != null)
                    {
                        foreach (KAnimElement element in animFrame.Elements)
                        {
                            var frame = KAnimBuildResolver.ResolveFrame(data.Build, element);
                            if (frame != null)
                            {
                                frames.Add(frame.GetTextureRectangle(data.Texture.PixelWidth, data.Texture.PixelHeight));
                                pivots.Add(frame.GetPivotPoint(data.Texture.PixelWidth, data.Texture.PixelHeight));
                            }
                        }
                    }
                    break;

                case KAnimElement element:
                    showTextureView = false;
                    if (data.Texture != null && data.Build != null)
                    {
                        var frame = KAnimBuildResolver.ResolveFrame(data.Build, element);
                        if (frame != null)
                        {
                            frames.Add(frame.GetTextureRectangle(data.Texture.PixelWidth, data.Texture.PixelHeight));
                            pivots.Add(frame.GetPivotPoint(data.Texture.PixelWidth, data.Texture.PixelHeight));
                        }
                    }
                    break;

                default:
                    break;


            }

            if (data.Texture != null && showTextureView)
            {
                UpdateTextureView(data.Texture, frames.ToArray(), pivots.ToArray()); // 更新框选
            }

            UpdateParameterInfo(selectedObj); // 更新详细信息
        }


        /// <summary>
        /// 更新左侧树视图内容，展示 build 和 anim 结构
        /// </summary>
        private void UpdateBuildTree(KAnimPackage data)
        {
            BuildTreeView.Items.Clear();
            if (data == null) return;

            if (data.Build != null)
            {
                var buildNode = CreateTreeItem(
                    $"Build  ({data.Build.SymbolCount} symbols, {data.Build.FrameCount} frames)",
                    data.Build,
                    PackIconKind.ImageMultipleOutline,
                    "Build");

                foreach (KSymbol symbol in data.Build.Symbols.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (!TreeMatches(symbol.Name))
                    {
                        continue;
                    }

                    var symbolNode = CreateTreeItem(
                        $"{symbol.Name}  ({symbol.FrameCount})",
                        symbol,
                        PackIconKind.ShapeOutline,
                        "Symbol");

                    foreach (KFrame frame in symbol.Frames)
                    {
                        var frameNode = CreateTreeItem(
                            $"Frame {frame.Index}  {frame.SpriteWidth}x{frame.SpriteHeight}",
                            frame,
                            PackIconKind.CropFree,
                            "Frame");
                        symbolNode.Items.Add(frameNode);
                    }

                    buildNode.Items.Add(symbolNode);
                }

                if (buildNode.Items.Count > 0 || string.IsNullOrWhiteSpace(treeSearchText))
                {
                    buildNode.IsExpanded = true;
                    BuildTreeView.Items.Add(buildNode);
                }
            }

            if (data.Anim != null)
            {
                var animNode = CreateTreeItem(
                    $"Animations  ({data.Anim.BankCount})",
                    data.Anim,
                    PackIconKind.AnimationPlayOutline,
                    "Anim");

                foreach (var bank in data.Anim.Banks.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var bankMatches = TreeMatches(bank.Name);
                    var shouldShowBank = bankMatches || string.IsNullOrWhiteSpace(treeSearchText) || bank.Frames.Any(FrameContainsMatchingElement);
                    if (!shouldShowBank)
                    {
                        continue;
                    }

                    var bankNode = CreateTreeItem(
                        $"{bank.Name}  ({bank.FrameCount} frames, {bank.Rate:0.##} fps)",
                        bank,
                        PackIconKind.MovieOpenPlayOutline,
                        "Bank");

                    if (bank.Frames.Count > 0)
                    {
                        bankNode.Items.Add(new TreeViewItem { Header = "展开加载帧..." });
                        bankNode.Expanded += BankNode_Expanded;
                    }

                    bankNode.IsExpanded = !string.IsNullOrWhiteSpace(treeSearchText);
                    animNode.Items.Add(bankNode);
                }

                if (animNode.Items.Count > 0 || string.IsNullOrWhiteSpace(treeSearchText))
                {
                    animNode.IsExpanded = true;
                    BuildTreeView.Items.Add(animNode);
                }
            }
        }

        private void SyncAnimationSelectionFromTree(TreeNodeTag nodeTag)
        {
            if (nodeTag.Bank == null || currentBank == null && AnimationComboBox == null)
            {
                return;
            }

            if (!ReferenceEquals(currentBank, nodeTag.Bank))
            {
                AnimationComboBox.SelectedItem = nodeTag.Bank;
                if (nodeTag.FrameIndex < 0)
                {
                    StartPlayback();
                }
            }

            if (nodeTag.FrameIndex >= 0)
            {
                currentFrameIndex = Math.Clamp(nodeTag.FrameIndex, 0, nodeTag.Bank.Frames.Count - 1);
                selectedElementIndex = nodeTag.ElementIndex;
                StopPlayback();
                RenderCurrentAnimationFrame();
            }
        }

        private void TreeSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            treeSearchText = TreeSearchBox.Text?.Trim() ?? string.Empty;
            if (data != null)
            {
                UpdateBuildTree(data);
            }
        }

        private bool TreeMatches(string? text)
        {
            return string.IsNullOrWhiteSpace(treeSearchText)
                || (!string.IsNullOrWhiteSpace(text) && text.Contains(treeSearchText, StringComparison.OrdinalIgnoreCase));
        }

        private bool FrameContainsMatchingElement(KAnimFrame frame)
        {
            if (data?.Build == null || string.IsNullOrWhiteSpace(treeSearchText))
            {
                return true;
            }

            return frame.Elements.Any(element => TreeMatches(data.Build.GetSymbol(element.SymbolHash)?.Name));
        }

        private void BankNode_Expanded(object sender, RoutedEventArgs e)
        {
            if (sender is not TreeViewItem bankNode ||
                bankNode.Tag is not TreeNodeTag { Value: KAnimBank bank })
            {
                return;
            }

            bankNode.Expanded -= BankNode_Expanded;
            bankNode.Items.Clear();
            bool bankMatches = TreeMatches(bank.Name);

            for (int i = 0; i < bank.Frames.Count; i++)
            {
                var frame = bank.Frames[i];
                if (!bankMatches && !FrameContainsMatchingElement(frame))
                {
                    continue;
                }

                var frameNode = CreateTreeItem(
                    $"Frame {i}  ({frame.Elements.Count} elements)",
                    new TreeNodeTag(frame, "AnimFrame", bank, i),
                    PackIconKind.LayersOutline,
                    "AnimFrame");

                for (int j = 0; j < frame.Elements.Count; j++)
                {
                    var element = frame.Elements[j];
                    var symbolName = data?.Build?.GetSymbol(element.SymbolHash)?.Name ?? $"#{element.SymbolHash}";
                    if (!bankMatches && !TreeMatches(symbolName))
                    {
                        continue;
                    }

                    var elementNode = CreateTreeItem(
                        $"{j}: {symbolName}  frame {element.FrameNumber}",
                        new TreeNodeTag(element, "Element", bank, i, j),
                        PackIconKind.VectorSquare,
                        "Element");
                    frameNode.Items.Add(elementNode);
                }

                bankNode.Items.Add(frameNode);
            }
        }

        private static object GetTreeItemValue(TreeViewItem item)
        {
            return item.Tag is TreeNodeTag nodeTag ? nodeTag.Value : item.Tag;
        }

        private static TreeViewItem CreateTreeItem(string title, object value, PackIconKind icon, string type)
        {
            return CreateTreeItem(title, new TreeNodeTag(value, type), icon, type);
        }

        private static TreeViewItem CreateTreeItem(string title, TreeNodeTag nodeTag, PackIconKind icon, string type)
        {
            var text = new TextBlock
            {
                Text = title,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };

            var header = new DockPanel { LastChildFill = true };
            header.Children.Add(new PackIcon
            {
                Kind = icon,
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            header.Children.Add(text);

            return new TreeViewItem
            {
                Header = header,
                Tag = nodeTag
            };
        }

        private sealed record TreeNodeTag(
            object Value,
            string Type,
            KAnimBank? Bank = null,
            int FrameIndex = -1,
            int ElementIndex = -1);

        private void InitializeAnimationPlayback()
        {
            StopPlayback();
            currentFrameIndex = 0;
            currentBank = null;
            selectedElementIndex = -1;

            isInitializingAnimationSelection = true;
            try
            {
                AnimationComboBox.ItemsSource = data?.Anim?.Banks;
                AnimationComboBox.SelectedIndex = GetPreferredAnimationIndex();
            }
            finally
            {
                isInitializingAnimationSelection = false;
            }

            if (AnimationComboBox.SelectedItem is KAnimBank selectedBank)
            {
                SetCurrentBank(selectedBank);
            }
            else
            {
                FrameStatusText.Text = "未加载动画";
                SetPlaybackControlsEnabled(false);
                UpdateFrameNavigator();
            }
        }

        private void SetCurrentBank(KAnimBank bank)
        {
            StopPlayback();
            currentBank = bank;
            currentFrameIndex = 0;
            selectedElementIndex = -1;
            SetPlaybackControlsEnabled(bank.Frames.Count > 0);
            UpdateFrameNavigator();
            UpdatePlaybackInterval();
            RenderCurrentAnimationFrame();
        }

        private int GetPreferredAnimationIndex()
        {
            var banks = data?.Anim?.Banks;
            if (banks == null || banks.Count == 0)
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

            var bestIndex = 0;
            var bestElementCount = -1;
            for (int i = 0; i < banks.Count; i++)
            {
                var elementCount = banks[i].Frames.Count > 0 ? banks[i].Frames[0].Elements.Count : 0;
                if (elementCount > bestElementCount)
                {
                    bestIndex = i;
                    bestElementCount = elementCount;
                }
            }

            return bestIndex;
        }

        private void SetPlaybackControlsEnabled(bool isEnabled)
        {
            PlayPauseButton.IsEnabled = isEnabled;
            PreviousFrameButton.IsEnabled = isEnabled;
            NextFrameButton.IsEnabled = isEnabled;
            FrameSlider.IsEnabled = isEnabled;
            PlaybackSpeedSlider.IsEnabled = isEnabled;
            FrameListBox.IsEnabled = isEnabled;
            ElementListBox.IsEnabled = isEnabled;
        }

        private void UpdatePlaybackInterval()
        {
            var rate = currentBank?.Rate > 0 ? currentBank.Rate : 30;
            playbackTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / (rate * playbackSpeedMultiplier));
        }

        private void PlaybackTimer_Tick(object? sender, EventArgs e)
        {
            StepFrame(1);
        }

        private void PlaybackSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            playbackSpeedMultiplier = Math.Clamp(e.NewValue, 0.1, 2.0);
            if (PlaybackSpeedText != null)
            {
                PlaybackSpeedText.Text = $"速度 {playbackSpeedMultiplier:0.00}x";
            }

            if (currentBank != null)
            {
                UpdatePlaybackInterval();
                FrameStatusText.Text = BuildFrameStatusText();
            }
        }

        private void AnimationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AnimationComboBox.SelectedItem is KAnimBank bank)
            {
                SetCurrentBank(bank);
                if (!isInitializingAnimationSelection)
                {
                    StartPlayback();
                }
            }
        }

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (currentBank == null || currentBank.Frames.Count == 0)
            {
                return;
            }

            if (isPlaying)
            {
                StopPlayback();
            }
            else
            {
                StartPlayback();
            }
        }

        private void PreviousFrame_Click(object sender, RoutedEventArgs e)
        {
            StopPlayback();
            StepFrame(-1);
        }

        private void NextFrame_Click(object sender, RoutedEventArgs e)
        {
            StopPlayback();
            StepFrame(1);
        }

        private void StopPlayback()
        {
            isPlaying = false;
            playbackTimer.Stop();

            if (PlayPauseButton != null)
            {
                PlayPauseButton.Content = new PackIcon { Kind = PackIconKind.Play };
            }
        }

        private void StartPlayback()
        {
            if (currentBank == null || currentBank.Frames.Count == 0)
            {
                return;
            }

            isPlaying = true;
            PlayPauseButton.Content = new PackIcon { Kind = PackIconKind.Pause };
            UpdatePlaybackInterval();
            playbackTimer.Start();
        }

        private void StepFrame(int delta)
        {
            if (currentBank == null || currentBank.Frames.Count == 0)
            {
                return;
            }

            currentFrameIndex = (currentFrameIndex + delta + currentBank.Frames.Count) % currentBank.Frames.Count;
            selectedElementIndex = -1;
            RenderCurrentAnimationFrame();
        }

        private void JumpToFrame(int frameIndex)
        {
            if (currentBank == null || currentBank.Frames.Count == 0)
            {
                return;
            }

            StopPlayback();
            currentFrameIndex = Math.Clamp(frameIndex, 0, currentBank.Frames.Count - 1);
            selectedElementIndex = -1;
            RenderCurrentAnimationFrame();
        }

        private void RenderCurrentAnimationFrame()
        {
            if (data?.Texture == null || data.Build == null || currentBank == null || currentBank.Frames.Count == 0)
            {
                return;
            }

            currentFrameIndex = Math.Clamp(currentFrameIndex, 0, currentBank.Frames.Count - 1);
            var frame = currentBank.Frames[currentFrameIndex];
            PreviewImage.Source = ShouldRenderInspectionOverlay()
                ? RenderAnimationFrame(frame, includeInspectionOverlay: true)
                : GetCachedAnimationFrame(currentBank, currentFrameIndex);
            SyncFrameSelection(frame);

            if (isPlaying)
            {
                var now = DateTime.UtcNow;
                if ((now - lastPlaybackStatusUpdate).TotalMilliseconds >= 250)
                {
                    FrameStatusText.Text = BuildFrameStatusText();
                    lastPlaybackStatusUpdate = now;
                }
            }
            else
            {
                FrameStatusText.Text = BuildFrameStatusText();
                UpdateParameterInfo(frame);
            }
        }

        private string BuildFrameStatusText()
        {
            if (currentBank == null)
            {
                return "未加载动画";
            }

            return $"{currentBank.Name}  {currentFrameIndex + 1}/{currentBank.Frames.Count}  {currentBank.Rate:0.##} fps · {playbackSpeedMultiplier:0.##}x";
        }

        private bool ShouldRenderInspectionOverlay()
        {
            return ShowOriginCheckBox.IsChecked == true ||
                ShowBoundsCheckBox.IsChecked == true ||
                (HighlightElementCheckBox.IsChecked == true && selectedElementIndex >= 0);
        }

        private void UpdateFrameNavigator()
        {
            isUpdatingFrameSelection = true;
            try
            {
                if (currentBank == null || currentBank.Frames.Count == 0)
                {
                    FrameSlider.Maximum = 0;
                    FrameSlider.Value = 0;
                    FrameListBox.ItemsSource = null;
                    ElementListBox.ItemsSource = null;
                    return;
                }

                FrameSlider.Maximum = currentBank.Frames.Count - 1;
                FrameSlider.Value = currentFrameIndex;
                FrameListBox.ItemsSource = currentBank.Frames
                    .Select((frame, index) => new FrameListItem
                    {
                        Index = index,
                        Title = $"{index}: {frame.Elements.Count}"
                    })
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
                FrameSlider.Value = currentFrameIndex;
                FrameListBox.SelectedIndex = currentFrameIndex;

                ElementListBox.ItemsSource = frame.Elements
                    .Select((element, index) => new ElementListItem
                    {
                        Index = index,
                        Element = element,
                        Title = $"{index}: {GetElementDisplayName(element)}"
                    })
                    .ToList();
                ElementListBox.SelectedIndex = selectedElementIndex >= 0 && selectedElementIndex < frame.Elements.Count
                    ? selectedElementIndex
                    : -1;
            }
            finally
            {
                isUpdatingFrameSelection = false;
            }
        }

        private string GetElementDisplayName(KAnimElement element)
        {
            var symbolName = data?.Build?.GetSymbol(element.SymbolHash)?.Name ?? element.SymbolHash.ToString();
            return $"{symbolName} f{element.FrameNumber}";
        }

        private void FrameSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isUpdatingFrameSelection || currentBank == null)
            {
                return;
            }

            JumpToFrame((int)Math.Round(e.NewValue));
        }

        private void FrameListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingFrameSelection || FrameListBox.SelectedItem is not FrameListItem item)
            {
                return;
            }

            JumpToFrame(item.Index);
        }

        private void ElementListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingFrameSelection)
            {
                return;
            }

            if (ElementListBox.SelectedItem is ElementListItem item)
            {
                selectedElementIndex = item.Index;
                UpdateParameterInfo(item.Element);
            }
            else
            {
                selectedElementIndex = -1;
            }

            RenderCurrentAnimationFrame();
        }

        private void OverlayOption_Changed(object sender, RoutedEventArgs e)
        {
            if (!isUpdatingFrameSelection && currentBank != null)
            {
                RenderCurrentAnimationFrame();
            }
        }

        private void PreviewViewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var factor = e.Delta > 0 ? 1.12 : 1 / 1.12;
            SetPreviewZoom(previewZoom * factor);
            e.Handled = true;
        }

        private void PreviewViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ResetPreviewTransform();
                e.Handled = true;
                return;
            }

            isPanningPreview = true;
            lastPreviewPanPoint = e.GetPosition(PreviewViewport);
            PreviewViewport.CaptureMouse();
            e.Handled = true;
        }

        private void PreviewViewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isPanningPreview)
            {
                return;
            }

            var current = e.GetPosition(PreviewViewport);
            var delta = current - lastPreviewPanPoint;
            PreviewTranslateTransform.X += delta.X;
            PreviewTranslateTransform.Y += delta.Y;
            lastPreviewPanPoint = current;
        }

        private void PreviewViewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isPanningPreview = false;
            PreviewViewport.ReleaseMouseCapture();
        }

        private void SetPreviewZoom(double value)
        {
            previewZoom = Math.Clamp(value, 0.25, 6.0);
            PreviewScaleTransform.ScaleX = previewZoom;
            PreviewScaleTransform.ScaleY = previewZoom;
        }

        private void ResetPreviewTransform()
        {
            SetPreviewZoom(1.0);
            PreviewTranslateTransform.X = 0;
            PreviewTranslateTransform.Y = 0;
        }

        private BitmapSource GetCachedAnimationFrame(KAnimBank bank, int frameIndex)
        {
            if (!animationFrameCache.TryGetValue(bank, out var frames))
            {
                frames = Enumerable.Repeat<BitmapSource?>(null, bank.Frames.Count).ToList();
                animationFrameCache[bank] = frames;
            }

            var frameImage = frames[frameIndex];
            if (frameImage == null)
            {
                frameImage = RenderAnimationFrame(bank.Frames[frameIndex], includeInspectionOverlay: false);
                frames[frameIndex] = frameImage;
                animationFrameCacheOrder.Enqueue((bank, frameIndex));
                TrimAnimationFrameCache();
            }

            return frameImage;
        }

        private void TrimAnimationFrameCache()
        {
            while (animationFrameCacheOrder.Count > MaxCachedAnimationFrames)
            {
                var oldest = animationFrameCacheOrder.Dequeue();
                if (animationFrameCache.TryGetValue(oldest.Bank, out var frames) &&
                    oldest.FrameIndex >= 0 &&
                    oldest.FrameIndex < frames.Count)
                {
                    frames[oldest.FrameIndex] = null;
                }
            }
        }

        private void ClearRenderCaches()
        {
            animationFrameCache.Clear();
            animationFrameCacheOrder.Clear();
            elementImageCache.Clear();
            elementImageCacheOrder.Clear();
        }

        private BitmapSource RenderAnimationFrame(KAnimFrame animFrame, bool includeInspectionOverlay)
        {
            const int canvasSize = AnimationCanvasSize;
            const double center = AnimationCanvasSize / 2.0;
            var contentBounds = CalculateFrameContentBounds(animFrame);
            var scale = CalculateAnimationScale(contentBounds, canvasSize);
            var offsetX = center - (contentBounds.Left + contentBounds.Width / 2.0) * scale;
            var offsetY = center - (contentBounds.Top + contentBounds.Height / 2.0) * scale;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, canvasSize, canvasSize));

                for (int i = animFrame.Elements.Count - 1; i >= 0; i--)
                {
                    DrawAnimationElement(dc, animFrame.Elements[i], offsetX, offsetY, scale);
                }

                if (includeInspectionOverlay)
                {
                    DrawInspectionOverlay(dc, animFrame, offsetX, offsetY, scale);
                }
            }

            var rtb = new RenderTargetBitmap(canvasSize, canvasSize, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }

        private static double CalculateAnimationScale(Rect bounds, int canvasSize)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return 1.0;
            }

            return Math.Min((canvasSize * 0.72) / bounds.Width, (canvasSize * 0.72) / bounds.Height);
        }

        private Rect CalculateFrameContentBounds(KAnimFrame animFrame)
        {
            Rect? bounds = null;

            foreach (var element in animFrame.Elements)
            {
                var elementBounds = CalculateElementBounds(element);
                if (elementBounds == Rect.Empty)
                {
                    continue;
                }

                bounds = bounds.HasValue ? Rect.Union(bounds.Value, elementBounds) : elementBounds;
            }

            return bounds ?? new Rect(-50, -50, 100, 100);
        }

        private Rect CalculateElementBounds(KAnimElement element)
        {
            if (data?.Texture == null || data.Build == null)
            {
                return Rect.Empty;
            }

            var buildFrame = KAnimBuildResolver.ResolveFrame(data.Build, element);
            if (buildFrame == null)
            {
                return Rect.Empty;
            }

            var localRect = GetBuildFrameLocalRect(buildFrame);
            var matrix = CreateExplorerElementMatrix(element, buildFrame);

            var topLeft = matrix.Transform(localRect.TopLeft);
            var topRight = matrix.Transform(localRect.TopRight);
            var bottomLeft = matrix.Transform(localRect.BottomLeft);
            var bottomRight = matrix.Transform(localRect.BottomRight);

            var left = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
            var top = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
            var right = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
            var bottom = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));

            return new Rect(new System.Windows.Point(left, top), new System.Windows.Point(right, bottom));
        }

        private void DrawAnimationElement(DrawingContext dc, KAnimElement element, double offsetX, double offsetY, double scale)
        {
            if (data?.Texture == null || data.Build == null)
            {
                return;
            }

            var buildFrame = KAnimBuildResolver.ResolveFrame(data.Build, element);
            if (buildFrame == null)
            {
                return;
            }

            var sourceRect = buildFrame.GetTextureRectangle(data.Texture.PixelWidth, data.Texture.PixelHeight);
            if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
            {
                return;
            }

            var cropped = GetCachedElementImage(buildFrame, sourceRect);
            var destination = GetBuildFrameLocalRect(buildFrame);

            dc.PushTransform(new TranslateTransform(offsetX, offsetY));
            dc.PushTransform(new ScaleTransform(scale, scale));
            dc.PushTransform(new MatrixTransform(CreateExplorerElementMatrix(element, buildFrame)));
            dc.PushOpacity(Math.Clamp(element.Alpha, 0, 1));

            dc.DrawImage(cropped, destination);

            dc.Pop();
            dc.Pop();
            dc.Pop();
            dc.Pop();
        }

        private void DrawInspectionOverlay(DrawingContext dc, KAnimFrame animFrame, double offsetX, double offsetY, double scale)
        {
            dc.PushTransform(new TranslateTransform(offsetX, offsetY));
            dc.PushTransform(new ScaleTransform(scale, scale));

            if (ShowBoundsCheckBox.IsChecked == true)
            {
                var bounds = CalculateFrameContentBounds(animFrame);
                var boundsPen = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(72, 210, 255)), 2 / scale)
                {
                    DashStyle = DashStyles.Dash
                };
                dc.DrawRectangle(null, boundsPen, bounds);
            }

            if (ShowOriginCheckBox.IsChecked == true)
            {
                var axisPen = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 238, 100)), 1.5 / scale);
                dc.DrawLine(axisPen, new System.Windows.Point(-28, 0), new System.Windows.Point(28, 0));
                dc.DrawLine(axisPen, new System.Windows.Point(0, -28), new System.Windows.Point(0, 28));
                dc.DrawEllipse(Brushes.Transparent, axisPen, new System.Windows.Point(0, 0), 5 / scale, 5 / scale);
            }

            if (HighlightElementCheckBox.IsChecked == true &&
                selectedElementIndex >= 0 &&
                selectedElementIndex < animFrame.Elements.Count)
            {
                DrawElementHighlight(dc, animFrame.Elements[selectedElementIndex], scale);
            }

            dc.Pop();
            dc.Pop();
        }

        private void DrawElementHighlight(DrawingContext dc, KAnimElement element, double scale)
        {
            if (data?.Build == null)
            {
                return;
            }

            var buildFrame = KAnimBuildResolver.ResolveFrame(data.Build, element);
            if (buildFrame == null)
            {
                return;
            }

            var localRect = GetBuildFrameLocalRect(buildFrame);
            var highlightPen = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 216, 0)), 3 / scale);
            var fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 216, 0));

            dc.PushTransform(new MatrixTransform(CreateExplorerElementMatrix(element, buildFrame)));
            dc.DrawRectangle(fill, highlightPen, localRect);
            dc.Pop();
        }

        private static Rect GetBuildFrameLocalRect(KFrame buildFrame)
        {
            return new Rect(
                -buildFrame.PivotWidth,
                -buildFrame.PivotHeight,
                buildFrame.PivotWidth * 2.0,
                buildFrame.PivotHeight * 2.0);
        }

        private static Matrix CreateExplorerElementMatrix(KAnimElement element, KFrame buildFrame)
        {
            var origin = TransformExplorerPoint(0, 0, element, buildFrame);
            var unitX = TransformExplorerPoint(1, 0, element, buildFrame);
            var unitY = TransformExplorerPoint(0, 1, element, buildFrame);

            return new Matrix(
                unitX.X - origin.X,
                unitX.Y - origin.Y,
                unitY.X - origin.X,
                unitY.Y - origin.Y,
                origin.X,
                origin.Y);
        }

        private static System.Windows.Point TransformExplorerPoint(double x, double y, KAnimElement element, KFrame buildFrame)
        {
            var pivotedX = x * 0.5 + buildFrame.PivotX;
            var pivotedY = y * 0.5 + buildFrame.PivotY;

            return new System.Windows.Point(
                pivotedX * element.M00 + pivotedY * element.M01 + element.M02,
                pivotedX * element.M10 + pivotedY * element.M11 + element.M12);
        }

        private BitmapSource GetCachedElementImage(KFrame frame, Rectangle sourceRect)
        {
            if (data?.Texture == null)
            {
                throw new InvalidOperationException("Texture is not loaded.");
            }

            var cacheKey = $"{frame.Parent.Hash}:{frame.Index}:{sourceRect.X}:{sourceRect.Y}:{sourceRect.Width}:{sourceRect.Height}";
            if (elementImageCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var cropped = new CroppedBitmap(data.Texture, new Int32Rect(sourceRect.X, sourceRect.Y, sourceRect.Width, sourceRect.Height));
            cropped.Freeze();
            elementImageCache[cacheKey] = cropped;
            elementImageCacheOrder.Enqueue(cacheKey);
            TrimElementImageCache();
            return cropped;
        }

        private void TrimElementImageCache()
        {
            while (elementImageCacheOrder.Count > MaxCachedElementImages)
            {
                var oldestKey = elementImageCacheOrder.Dequeue();
                elementImageCache.Remove(oldestKey);
            }
        }


        private void UpdateParameterInfo(object selectedObj)
        {
            var list = new List<KeyValueItem>();

            switch (selectedObj)
            {
                case KBuild build:
                    list.Add(new KeyValueItem { Key = "Build 名称", Value = build.Name }); 
                    list.Add(new KeyValueItem { Key = "Symbol 数量", Value = build.SymbolCount.ToString() });
                    list.Add(new KeyValueItem { Key = "Frame 总数", Value = build.FrameCount.ToString() });
                    break;

                case KSymbol symbol:
                    list.Add(new KeyValueItem { Key = "Symbol 名称", Value = symbol.Name });
                    list.Add(new KeyValueItem { Key = "Hash", Value = symbol.Hash.ToString() });
                    list.Add(new KeyValueItem { Key = "帧数量", Value = (symbol.Frames?.Count ?? 0).ToString() });
                  
                  
                    break;

                case KFrame frame:
                    list.Add(new KeyValueItem { Key = "Frame 索引", Value = frame.Index.ToString() });
                    list.Add(new KeyValueItem { Key = "持续时间(ms)", Value = frame.Duration.ToString() });
                    if (data?.Texture != null)
                    {
                        var rect = frame.GetTextureRectangle(data.Texture.PixelWidth, data.Texture.PixelHeight);
                        list.Add(new KeyValueItem { Key = "纹理区域", Value = $"{rect.X},{rect.Y},{rect.Width},{rect.Height}" });
                        var pivot = frame.GetPivotPoint(data.Texture.PixelWidth, data.Texture.PixelHeight);
                        list.Add(new KeyValueItem { Key = "锚点", Value = $"{pivot.X:F2},{pivot.Y:F2}" });
                    }
                    break;


                case KAnimFrame animFrame:
                    list.Add(new KeyValueItem { Key = "X", Value = animFrame.X.ToString("F2") });
                    list.Add(new KeyValueItem { Key = "Y", Value = animFrame.Y.ToString("F2") });
                    list.Add(new KeyValueItem { Key = "宽度", Value = animFrame.Width.ToString("F2") });
                    list.Add(new KeyValueItem { Key = "高度", Value = animFrame.Height.ToString("F2") });
                    list.Add(new KeyValueItem { Key = "元素数量", Value = animFrame.ElementCount.ToString() });
                    break;


                case KAnimElement element:
                    list.Add(new KeyValueItem { Key = "SymbolHash", Value = element.SymbolHash.ToString() });
                    list.Add(new KeyValueItem { Key = "FrameNumber", Value = element.FrameNumber.ToString() });
                    list.Add(new KeyValueItem { Key = "FolderHash", Value = element.FolderHash.ToString() });
                    list.Add(new KeyValueItem { Key = "Flags", Value = element.Flags.ToString() });
                    list.Add(new KeyValueItem { Key = "R", Value = element.Red.ToString("F2") });
                    list.Add(new KeyValueItem { Key = "G", Value = element.Green.ToString("F2") });
                    list.Add(new KeyValueItem { Key = "B", Value = element.Blue.ToString("F2") });
                    list.Add(new KeyValueItem { Key = "A", Value = element.Alpha.ToString("F2") });
                    list.Add(new KeyValueItem { Key = "M00", Value = element.M00.ToString("F2") });
                    list.Add(new KeyValueItem { Key = "M10", Value = element.M10.ToString("F2") });
                    list.Add(new KeyValueItem { Key = "M01", Value = element.M01.ToString("F2") });
                    list.Add(new KeyValueItem { Key = "M11", Value = element.M11.ToString("F2") });
                    list.Add(new KeyValueItem { Key = "M02", Value = element.M02.ToString("F2") });
                    list.Add(new KeyValueItem { Key = "M12", Value = element.M12.ToString("F2") });
                    list.Add(new KeyValueItem { Key = "Unused", Value = element.Unused.ToString("F2") });
                    break;


                default:
                    list.Add(new KeyValueItem { Key = "无可用参数信息", Value = "" });
                    break;
            }

            ParameterDataGrid.ItemsSource = list;
        }

        /// <summary>
        /// 绘制图片预览，显示纹理和选中帧的红色框及绿色锚点
        /// </summary>
        private void UpdateTextureView(BitmapImage? img, Rectangle[]? frames = null, PointF[]? pivots = null)
        {
            if (img == null)
            {
                PreviewImage.Source = null;
                return;
            }

            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                // 画原始图片
                dc.DrawImage(img, new Rect(0, 0, img.PixelWidth, img.PixelHeight));

                // 画红色矩形框
                if (frames != null)
                {
                    System.Windows.Media.Pen redPen = new System.Windows.Media.Pen(Brushes.Red, 2);
                    foreach (var frame in frames)
                    {
                        if (frame != Rectangle.Empty)
                        {
                            dc.DrawRectangle(null, redPen, new Rect(frame.Left, frame.Top, frame.Width, frame.Height));
                        }
                    }
                }

                // 画绿色锚点小方块
                if (pivots != null)
                {
                    Brush greenBrush = Brushes.LimeGreen;
                    foreach (var pivot in pivots)
                    {
                        if (pivot != PointF.Empty)
                        {
                            dc.DrawRectangle(greenBrush, null, new Rect(pivot.X - 1.5, pivot.Y - 1.5, 3, 3));
                        }
                    }
                }

                // 如果需要重新打包提示
                if (data?.Build?.NeedsRepack == true)
                {
                    FormattedText text = new FormattedText(
                        "Requires Rebuild",
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        _typeface,
                        _fontSize,
                        Brushes.Orange,
                        1.0);

                    dc.DrawText(text, new System.Windows.Point(5, 5));
                }
            }

            RenderTargetBitmap rtb = new RenderTargetBitmap(
                img.PixelWidth,
                img.PixelHeight,
                img.DpiX,
                img.DpiY,
                PixelFormats.Pbgra32);

            rtb.Render(visual);
            PreviewImage.Source = rtb;
        }



        // 右键点击时，先把点击的 TreeViewItem 选中
        private void TreeViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem item)
            {
                item.IsSelected = true;  
                e.Handled = false;       
            }
        }


        /// <summary>
        /// 导出当前选中的 Frame 或 Symbol 的贴图区域为 PNG 图片文件。
        /// 若选择的是 Symbol，则导出其第一个 Frame 对应区域。
        /// </summary>
        /// <param name="sender">事件源对象，通常为菜单项或按钮。</param>
        /// <param name="e">事件参数。</param>
        private void ExportSelectedImage_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = BuildTreeView.SelectedItem as TreeViewItem;
            if (selectedItem == null) return;

            var selectedObj = GetTreeItemValue(selectedItem);

            if (selectedObj == null)
            {
                MessageBox.Show("请先选择一个节点");
                return;
            }

            var texture = data?.Texture;
            if (texture == null)
            {
                MessageBox.Show("当前没有可用的贴图");
                return;
            }

            List<Rectangle> frames = new List<Rectangle>();
            List<PointF> pivots = new List<PointF>();

            switch (selectedObj)
            {
                case KSymbol symbol:
                    foreach (var frame in symbol.Frames)
                    {
                        frames.Add(frame.GetTextureRectangle(texture.PixelWidth, texture.PixelHeight));
                        pivots.Add(frame.GetPivotPoint(texture.PixelWidth, texture.PixelHeight));
                    }
                    break;

                case KFrame frame:
                    frames.Add(frame.GetTextureRectangle(texture.PixelWidth, texture.PixelHeight));
                    pivots.Add(frame.GetPivotPoint(texture.PixelWidth, texture.PixelHeight));
                    break;

                case KBuild build:
                    MessageBox.Show("导出整张贴图不支持（建议导出 Symbol 或 Frame）");
                    return;

                default:
                    MessageBox.Show("请选择一个 Symbol 或 Frame 导出图片");
                    return;
            }

            if (frames.Count == 0)
            {
                MessageBox.Show("未找到可导出的区域");
                return;
            }

            // 导出第一块区域（你之前的裁剪逻辑）
            var rect = frames[0];

            try
            {
                var cropped = new CroppedBitmap(texture, new Int32Rect(rect.X, rect.Y, rect.Width, rect.Height));

                var saveDlg = new Microsoft.Win32.SaveFileDialog()
                {
                    Filter = "PNG Image|*.png",
                    FileName = "export.png"
                };

                if (saveDlg.ShowDialog() == true)
                {
                    using (var fileStream = new FileStream(saveDlg.FileName, FileMode.Create))
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(cropped));
                        encoder.Save(fileStream);
                    }
                    MessageBox.Show("导出成功！");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("导出失败：" + ex.Message);
            }
        }



        /// <summary>
        /// 导出当前替换后的整张贴图为 PNG 文件。
        /// </summary>
        /// <param name="sender">事件触发对象，通常为按钮。</param>
        /// <param name="e">事件参数。</param>
        private void MenuExportPng_Click(object sender, RoutedEventArgs e)
        {
            var texture = data?.Texture;
            if (texture == null)
            {
                MessageBox.Show("当前没有可用的贴图");
                return;
            }

            try
            {
                var saveDlg = new Microsoft.Win32.SaveFileDialog()
                {
                    Filter = "PNG Image|*.png",
                    FileName = "texture.png"
                };

                if (saveDlg.ShowDialog() == true)
                {
                    using (var fileStream = new FileStream(saveDlg.FileName, FileMode.Create))
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(texture));
                        encoder.Save(fileStream);
                    }
                    MessageBox.Show("整张贴图导出成功！");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("导出失败：" + ex.Message);
            }
        }


        /// <summary>
        /// 打开Kanim文件
        /// </summary>
       
        private void MenuOpen_Click(object sender, RoutedEventArgs e)
        {
            // 判断当前是否有已加载文件
            bool hasFiles = !string.IsNullOrEmpty(currentTextureFile)
                            || !string.IsNullOrEmpty(currentAnimFile)
                            || !string.IsNullOrEmpty(currentBuildFile);

            if (hasFiles)
            {
                // 清空显示区域
                ContentPanel.Children.Clear();

                // 恢复默认图标和提示
                UploadIcon.Kind = PackIconKind.FileUpload;
                HintText.Text = "拖放 .png、_anim、_build 文件到此处";

                ContentPanel.Children.Add(UploadIcon);
                ContentPanel.Children.Add(HintText);

                // 清空文件路径
                currentTextureFile = null;
                currentAnimFile = null;
                currentBuildFile = null;

                // 清空参数数据表
                ParameterDataGrid.ItemsSource = null;

                // 清空树视图
                BuildTreeView.Items.Clear();

                // 清空预览图
                PreviewImage.Source = null;

                // 清空数据
                data = null;
            }
            else
            {
                // 打开文件选择对话框
                var dlg = new OpenFileDialog
                {
                    Multiselect = true,
                    Filter = "KAnim files|*.png;*_anim.bytes;*_build.bytes|所有文件|*.*"
                };

                if (dlg.ShowDialog() == true)
                {
                    // 处理选择文件路径
                    foreach (var file in dlg.FileNames)
                    {
                        var filename = Path.GetFileName(file).ToLowerInvariant();
                        if (filename.EndsWith(".png"))
                            currentTextureFile = file;
                        else if (filename.EndsWith("_anim.bytes"))
                            currentAnimFile = file;
                        else if (filename.EndsWith("_build.bytes"))
                            currentBuildFile = file;
                    }

                    var displayFiles = new List<(string, PackIconKind)>();

                    if (!string.IsNullOrEmpty(currentTextureFile))
                        displayFiles.Add((Path.GetFileName(currentTextureFile), PackIconKind.FileImageOutline));
                    if (!string.IsNullOrEmpty(currentAnimFile))
                        displayFiles.Add((Path.GetFileName(currentAnimFile), PackIconKind.FileDocumentOutline));
                    if (!string.IsNullOrEmpty(currentBuildFile))
                        displayFiles.Add((Path.GetFileName(currentBuildFile), PackIconKind.FileDocumentOutline));

                    ShowFileList(displayFiles);

                    if (!string.IsNullOrEmpty(currentTextureFile) &&
                        !string.IsNullOrEmpty(currentAnimFile) &&
                        !string.IsNullOrEmpty(currentBuildFile))
                    {
                        OpenFiles(currentTextureFile, currentBuildFile, currentAnimFile);
                    }
                    else
                    {
                        MessageBox.Show("请同时选择 .png、_anim.bytes 和 _build.bytes 文件", "缺少文件", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }

        private void DiagnosePackage_Click(object sender, RoutedEventArgs e)
        {
            if (data == null || !data.HasAnyData)
            {
                MessageBox.Show("请先打开一组 KAnim 文件。", "KAnim 诊断", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var diagnostics = KAnimDiagnostics.Analyze(data);
            var report = KAnimDiagnostics.FormatReport(data, diagnostics);
            MessageBox.Show(report, "KAnim 诊断", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        /// <summary>
        /// 替换当前选中 Frame 或 Symbol 的贴图区域为用户选择的新图片。
        /// 用户选择的图片会被缩放到对应 Frame 大小并写入原贴图中，再刷新显示。
        /// </summary>
        /// <param name="sender">事件源对象，通常是按钮。</param>
        /// <param name="e">事件参数。</param>
        private void ReplaceSelectedImage_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = BuildTreeView.SelectedItem as TreeViewItem;
            if (selectedItem?.Tag == null)
            {
                MessageBox.Show("请先选择一个节点");
                return;
            }

            var selectedObj = GetTreeItemValue(selectedItem);
            KFrame? frameToReplace = null;

            switch (selectedObj)
            {
                case KFrame frame:
                    frameToReplace = frame;
                    break;

                case KSymbol symbol:
                    if (symbol.Frames.Count > 0)
                        frameToReplace = symbol.Frames[0];
                    break;

                default:
                    MessageBox.Show("请选择一个 Frame 或 Symbol 节点");
                    return;
            }

            if (frameToReplace == null)
            {
                MessageBox.Show("找不到要替换的 Frame");
                return;
            }

            var texture = data?.Texture;
            if (data == null || texture == null)
            {
                MessageBox.Show("当前没有可用的贴图");
                return;
            }

            var frameRect = frameToReplace.GetTextureRectangle(texture.PixelWidth, texture.PixelHeight);

            // 打开图片选择窗口
            var openDlg = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg",
                Title = "选择一张图片用于替换 Frame"
            };

            if (openDlg.ShowDialog() != true) return;

            try
            {
                // 加载原图
                var originalImage = new BitmapImage(new Uri(openDlg.FileName));

                // 缩放到 Frame 区域尺寸
                var scaled = new TransformedBitmap(originalImage, new ScaleTransform(
                    (double)frameRect.Width / originalImage.PixelWidth,
                    (double)frameRect.Height / originalImage.PixelHeight
                ));
                scaled.Freeze();

                // 将原贴图转为 WriteableBitmap
                var writeable = new WriteableBitmap(texture);

                int stride = frameRect.Width * 4;
                byte[] pixels = new byte[stride * frameRect.Height];
                scaled.CopyPixels(pixels, stride, 0);

                // 写入新的像素数据
                var int32Rect = new Int32Rect(frameRect.X, frameRect.Y, frameRect.Width, frameRect.Height);
                writeable.WritePixels(int32Rect, pixels, stride, 0);

                // 转回 BitmapImage 并赋值回去
                data.Texture = ConvertWriteableToBitmapImage(writeable);


                // 替换后立即刷新界面
                UpdateTextureView(
                    data.Texture,
                    new[] { frameRect },
                    new[] { frameToReplace.GetPivotPoint(data.Texture.PixelWidth, data.Texture.PixelHeight) }
                );

                MessageBox.Show("替换成功！");
            }
            catch (Exception ex)
            {
                MessageBox.Show("替换失败：" + ex.Message);
            }
        }


        /// <summary>
        /// 将 <see cref="WriteableBitmap"/> 转换为 <see cref="BitmapImage"/>。
        /// </summary>
        /// <param name="writeable">需要转换的 <see cref="WriteableBitmap"/> 实例。</param>
        /// <returns>转换后的 <see cref="BitmapImage"/> 对象。</returns>
        private BitmapImage ConvertWriteableToBitmapImage(WriteableBitmap writeable)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(writeable));

            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                ms.Position = 0;

                var bmpImg = new BitmapImage();
                bmpImg.BeginInit();
                bmpImg.CacheOption = BitmapCacheOption.OnLoad;
                bmpImg.StreamSource = ms;
                bmpImg.EndInit();
                bmpImg.Freeze(); 

                return bmpImg;
            }
        }


        /// <summary>
        /// 关闭当前窗口
        /// </summary>
        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();

        }


        /// <summary>
        /// 双击拖放区域时触发
        /// 清空所有加载的数据和界面显示
        /// </summary>
        private void DropCard_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 判断当前是否有已加载文件
            bool hasFiles = !string.IsNullOrEmpty(currentTextureFile)
                            || !string.IsNullOrEmpty(currentAnimFile)
                            || !string.IsNullOrEmpty(currentBuildFile);

            if (hasFiles)
            {
                // 清空显示区域
                ContentPanel.Children.Clear();

                // 恢复默认图标和提示
                UploadIcon.Kind = PackIconKind.FileUpload;
                HintText.Text = "拖放 .png、_anim、_build 文件到此处";

                ContentPanel.Children.Add(UploadIcon);
                ContentPanel.Children.Add(HintText);

                // 清空文件路径
                currentTextureFile = null;
                currentAnimFile = null;
                currentBuildFile = null;


                
                // 清空参数数据表
                ParameterDataGrid.ItemsSource = null;

                // 清空树视图
                BuildTreeView.Items.Clear();

                // 清空预览图
                PreviewImage.Source = null;

                // 清空数据
                data = null;
            }
            else
            {
                // 如果无文件，弹出文件选择对话框
                var dlg = new OpenFileDialog
                {
                    Multiselect = true,
                    Filter = "KAnim files|*.png;*_anim.bytes;*_build.bytes|所有文件|*.*"
                };

                if (dlg.ShowDialog() == true)
                {
                    // 处理选择文件路径
                    foreach (var file in dlg.FileNames)
                    {
                        var filename = Path.GetFileName(file).ToLowerInvariant();
                        if (filename.EndsWith(".png"))
                            currentTextureFile = file;
                        else if (filename.EndsWith("_anim.bytes"))
                            currentAnimFile = file;
                        else if (filename.EndsWith("_build.bytes"))
                            currentBuildFile = file;
                    }

                    var displayFiles = new List<(string, PackIconKind)>();

                    if (!string.IsNullOrEmpty(currentTextureFile))
                        displayFiles.Add((Path.GetFileName(currentTextureFile), PackIconKind.FileImageOutline));
                    if (!string.IsNullOrEmpty(currentAnimFile))
                        displayFiles.Add((Path.GetFileName(currentAnimFile), PackIconKind.FileDocumentOutline));
                    if (!string.IsNullOrEmpty(currentBuildFile))
                        displayFiles.Add((Path.GetFileName(currentBuildFile), PackIconKind.FileDocumentOutline));

                    ShowFileList(displayFiles);

                    if (!string.IsNullOrEmpty(currentTextureFile) &&
                        !string.IsNullOrEmpty(currentAnimFile) &&
                        !string.IsNullOrEmpty(currentBuildFile))
                    {
                        OpenFiles(currentTextureFile, currentBuildFile, currentAnimFile);
                    }
                    else
                    {
                        MessageBox.Show("请同时选择 .png、_anim.bytes 和 _build.bytes 文件", "缺少文件", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }


        /// <summary>
        /// 鼠标悬停时显示提示文本
        /// </summary>
        private void DropCard_MouseEnter(object sender, MouseEventArgs e)
        {
            bool hasFiles = !string.IsNullOrEmpty(currentTextureFile)
                            || !string.IsNullOrEmpty(currentAnimFile)
                            || !string.IsNullOrEmpty(currentBuildFile);

            if (sender is MaterialDesignThemes.Wpf.Card card)
            {
                card.ToolTip = hasFiles ? "双击清空内容" : "双击选择文件";
            }
        }



    }
}
