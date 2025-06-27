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
using KanimLib;
using MaterialDesignThemes.Wpf;
using Brushes = System.Windows.Media.Brushes;
using Brush = System.Windows.Media.Brush;

namespace KAnimGui.Windos
{
    public partial class KAnimRenderWindow : Window
    {
        // 当前加载的数据包，包含纹理、build、anim
        private KAnimPackage data;

        // 当前打开的文件路径（纹理、动画、构建文件）
        private string currentTextureFile = null;
        private string currentAnimFile = null;
        private string currentBuildFile = null;

        // 用于绘制文本的字体和大小
        private Typeface _typeface = new Typeface("Segoe UI");
        private double _fontSize = 14;

        public KAnimRenderWindow()
        {
            InitializeComponent();
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
                    Icon.Kind = PackIconKind.CloudDownload;
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
                card.Background = (Brush)FindResource("MaterialDesignPaper");
                // 恢复默认图标和提示
                Icon.Kind = PackIconKind.FileUpload;
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
                card.Background = (Brush)FindResource("MaterialDesignPaper");
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
        private void OpenFiles(string textureFile, string buildFile, string animFile)
        {
            BitmapImage texture = null;
            KBuild build = null;
            KAnim anim = null;

            // 加载 PNG 纹理图
            if (!string.IsNullOrEmpty(textureFile) && File.Exists(textureFile))
            {
                texture = new BitmapImage();
                texture.BeginInit();
                texture.CacheOption = BitmapCacheOption.OnLoad;
                texture.UriSource = new Uri(textureFile, UriKind.Absolute);
                texture.EndInit();
            }

            // 读取 build 数据
            if (!string.IsNullOrEmpty(buildFile) && File.Exists(buildFile))
            {
                build = KAnimUtils.ReadBuild(buildFile);
            }

            // 读取 anim 数据
            if (!string.IsNullOrEmpty(animFile) && File.Exists(animFile))
            {
                anim = KAnimUtils.ReadAnim(animFile);

                // 修复字符串索引等信息
                if (build != null)
                {
                    anim.RepairStringsFromBuild(build);
                }
            }

            // 把读取的数据传给界面更新显示
            OpenData(texture, build, anim);
        }

        /// <summary>
        /// 更新界面显示：保存数据包，更新预览和树视图
        /// </summary>
        private void OpenData(BitmapImage texture, KBuild build, KAnim anim)
        {
            data = new KAnimPackage
            {
                Texture = texture,
                Build = build,
                Anim = anim
            };

            UpdateAtlasView(data.Texture);
            UpdateBuildTree(data);
        }

        /// <summary>
        /// 绘制图片预览，显示纹理和选中帧的红色框及绿色锚点
        /// </summary>
        private void UpdateAtlasView(BitmapImage img, Rectangle[] frames = null, PointF[] pivots = null)
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

        /// <summary>
        /// 更新左侧树视图内容，展示 build 和 anim 结构
        /// </summary>
        private void UpdateBuildTree(KAnimPackage data)
        {
            BuildTreeView.Items.Clear();
            if (data == null) return;

            // Build 节点及其子节点
            if (data.Build != null)
            {
                var buildNode = new TreeViewItem { Header = data.Build.ToString(), Tag = data.Build };

                foreach (KSymbol symbol in data.Build.Symbols)
                {
                    var symbolNode = new TreeViewItem { Header = symbol.Name, Tag = symbol };

                    foreach (KFrame frame in symbol.Frames)
                    {
                        var frameNode = new TreeViewItem { Header = $"Frame {frame.Index}", Tag = frame };
                        symbolNode.Items.Add(frameNode);
                    }

                    buildNode.Items.Add(symbolNode);
                }

                BuildTreeView.Items.Add(buildNode);
            }

            // Anim 节点及其子节点
            if (data.Anim != null)
            {
                var animNode = new TreeViewItem { Header = "Animations", Tag = data.Anim };

                foreach (var bank in data.Anim.Banks)
                {
                    var bankNode = new TreeViewItem { Header = bank.Name, Tag = bank };

                    for (int i = 0; i < bank.Frames.Count; i++)
                    {
                        var frame = bank.Frames[i];
                        var frameNode = new TreeViewItem { Header = $"Frame {i}", Tag = frame };

                        for (int j = 0; j < frame.Elements.Count; j++)
                        {
                            var element = frame.Elements[j];
                            var elementNode = new TreeViewItem { Header = $"Element {j}", Tag = element };
                            frameNode.Items.Add(elementNode);
                        }

                        bankNode.Items.Add(frameNode);
                    }

                    animNode.Items.Add(bankNode);
                }

                BuildTreeView.Items.Add(animNode);
            }
        }

        /// <summary>
        /// 树节点选中事件，根据不同类型高亮显示对应的区域
        /// </summary>
        private void BuildTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selectedItem = BuildTreeView.SelectedItem as TreeViewItem;
            if (selectedItem == null) return;

            var selectedObj = selectedItem.Tag;

            List<Rectangle> frames = new List<Rectangle>();
            List<PointF> pivots = new List<PointF>();

            switch (selectedObj)
            {
                case KBuild build:
                    // 不绘制任何选中框
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

                default:
                    break;
            }

            if (data.Texture != null)
            {
                UpdateAtlasView(data.Texture, frames.ToArray(), pivots.ToArray());

                // 新增：顺带导出当前选中图片
               //  ExportSelectedImage(selectedObj, frames);
            }
        }

        /// <summary>
        /// 导出选中对象的贴图区域图片
        /// </summary>
        private void ExportSelectedImage(object selectedObj, List<Rectangle> frames)
        {
            if (frames == null || frames.Count == 0)
            {
                MessageBox.Show("未找到可导出的区域");
                return;
            }

            var rect = frames[0]; // 这里只导出第一个区域

            try
            {
                var bitmapSource = data.Texture;
                var cropped = new CroppedBitmap(bitmapSource, new Int32Rect(rect.X, rect.Y, rect.Width, rect.Height));

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










        // 右键点击时，先把点击的 TreeViewItem 选中
        private void TreeViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem item)
            {
                item.IsSelected = true;  // 👈关键点：右键时设为选中
                e.Handled = false;       // 允许右键菜单继续弹出
            }
        }


        // 导出菜单点击事件，沿用你原来的逻辑
        private void ExportSelectedImage_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = BuildTreeView.SelectedItem as TreeViewItem;
            if (selectedItem == null) return;

            var selectedObj = selectedItem.Tag;

            if (selectedObj == null)
            {
                MessageBox.Show("请先选择一个节点");
                return;
            }

            List<Rectangle> frames = new List<Rectangle>();
            List<PointF> pivots = new List<PointF>();

            switch (selectedObj)
            {
                case KSymbol symbol:
                    if (data.Texture != null)
                    {
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
                        frames.Add(frame.GetTextureRectangle(data.Texture.PixelWidth, data.Texture.PixelHeight));
                        pivots.Add(frame.GetPivotPoint(data.Texture.PixelWidth, data.Texture.PixelHeight));
                    }
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
                var bitmapSource = data.Texture;
                var cropped = new CroppedBitmap(bitmapSource, new Int32Rect(rect.X, rect.Y, rect.Width, rect.Height));

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
                Icon.Kind = PackIconKind.FileUpload;
                HintText.Text = "拖放 .png、_anim、_build 文件到此处";

                ContentPanel.Children.Add(Icon);
                ContentPanel.Children.Add(HintText);

                // 清空文件路径
                currentTextureFile = null;
                currentAnimFile = null;
                currentBuildFile = null;

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
