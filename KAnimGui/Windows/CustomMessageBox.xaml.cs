using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MaterialDesignThemes.Wpf;

namespace KAnimGui.Windows
{
    /// <summary>
    /// CustomMessageBox.xaml 的交互逻辑
    /// </summary>
    public partial class CustomMessageBox : Window
    {
        /// <summary>
        /// 无参构造函数：主要用于 XAML 设计器预览或默认弹出。
        /// 通过 : this() 语法将调用引导至下方的有参构造函数。
        /// </summary>
        public CustomMessageBox() : this("操作完成", "提示", PackIconKind.Information)
        {
            // InitializeComponent();
            // LoadReadmeAsync();
        }

        /// <summary>
        /// 初始化自定义对话框实例，并支持自定义文本、标题和图标。
        /// </summary>
        /// <param name="message">要在文本框中显示的主要消息内容。支持长文本换行。</param>
        /// <param name="title">窗口标题栏显示的文字。</param>
        /// <param name="iconKind">要显示的图标类型（如 Success, Error, Information）。</param>
        public CustomMessageBox(string message, string title, PackIconKind iconKind)
        {
            InitializeComponent();

            // 1. 设置基本内容属性
            this.Title = title;
            this.ReadmeTextBox.Text = message;
            this.IconPack.Kind = iconKind;

            // --- 标题样式：要粗 ---
            this.TitleTextBlock.FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI");
            this.TitleTextBlock.FontSize = 18;
            this.TitleTextBlock.FontWeight = FontWeights.Bold;
            this.TitleTextBlock.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#333333"));
            // 渲染优化
            System.Windows.Media.TextOptions.SetTextFormattingMode(this.TitleTextBlock, System.Windows.Media.TextFormattingMode.Display);
            System.Windows.Media.TextOptions.SetTextRenderingMode(this.TitleTextBlock, System.Windows.Media.TextRenderingMode.ClearType);



            // --- 内容样式：要细 ---
            this.ReadmeTextBox.FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI");
            this.ReadmeTextBox.FontSize = 14;
            this.ReadmeTextBox.FontWeight = FontWeights.Normal; // 确保内容是常规细度

            // 颜色与交互设置（淡灰色，不可选中）
            var grayColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#555555");
            this.ReadmeTextBox.Foreground = new System.Windows.Media.SolidColorBrush(grayColor);
            this.ReadmeTextBox.Focusable = false;
            this.ReadmeTextBox.Cursor = System.Windows.Input.Cursors.Arrow;
            this.ReadmeTextBox.IsHitTestVisible = false;

            // 渲染优化
            System.Windows.Media.TextOptions.SetTextFormattingMode(this.ReadmeTextBox, System.Windows.Media.TextFormattingMode.Display);
            System.Windows.Media.TextOptions.SetTextRenderingMode(this.ReadmeTextBox, System.Windows.Media.TextRenderingMode.ClearType);


            // 3. 根据图标类型进行视觉反馈调整
            ApplyVisualFeedback(iconKind);
        }

        /// <summary>
        /// 根据图标类型自动调整 UI 细节（内部辅助方法）。
        /// </summary>
        /// <param name="kind">图标类型</param>
        private void ApplyVisualFeedback(PackIconKind kind)
        {
            switch (kind)
            {
                case PackIconKind.CheckCircle:
                    // 成功：可以保持默认或设置特定样式
                    break;
                case PackIconKind.CloseCircle:
                    // 错误：加粗显示文本，增强视觉警示
                    this.ReadmeTextBox.FontWeight = FontWeights.Bold;
                    break;
                case PackIconKind.AlertCircle:
                    // 警告或异常
                    break;
            }
        }






        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();  // 开始拖动窗口
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // 双击时关闭窗口
                this.Close();
            }
        }



        private void Button_Click(object sender, RoutedEventArgs e)
        {
            
            // this.Close();
        }

    }































    //private void Button_Click(object sender, RoutedEventArgs e)
    //{

    //    string url = "https://github.com/ChiYuKe/KAnim_GUI/tree/master/KAnimGui";
    //    try
    //    {
    //        Process.Start(new ProcessStartInfo
    //        {
    //            FileName = url,
    //            UseShellExecute = true
    //        });
    //    }
    //    catch (Exception ex)
    //    {
    //        MessageBox.Show("无法打开网页: " + ex.Message);
    //    }
    //}

    //private async void LoadReadmeAsync()
    //{
    //    string url = "https://raw.githubusercontent.com/ChiYuKe/KAnim_GUI/refs/heads/master/README.md";
    //    try
    //    {
    //        using var client = new HttpClient();

    //        // 添加禁止缓存的请求头
    //        client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
    //        {
    //            NoCache = true,
    //            NoStore = true,
    //            MustRevalidate = true
    //        };

    //        // 添加时间戳避免缓存（也可选用）
    //        var fullUrl = url + "?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    //        string content = await client.GetStringAsync(fullUrl);
    //        ReadmeTextBox.Text = content;
    //    }
    //    catch (Exception ex)
    //    {
    //        ReadmeTextBox.Text = $"加载失败：{ex.Message}";
    //    }
    //}



}
