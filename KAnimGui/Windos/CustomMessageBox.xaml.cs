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

namespace KAnimGui.Windos
{
    /// <summary>
    /// CustomMessageBox.xaml 的交互逻辑
    /// </summary>
    public partial class CustomMessageBox : Window
    {
        public CustomMessageBox()
        {
            InitializeComponent();
            LoadReadmeAsync();


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

            string url = "https://github.com/ChiYuKe/KAnim_GUI/tree/master/KAnimGui";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法打开网页: " + ex.Message);
            }
        }

        private async void LoadReadmeAsync()
        {
            string url = "https://raw.githubusercontent.com/ChiYuKe/KAnim_GUI/refs/heads/master/README.md";
            try
            {
                using var client = new HttpClient();

                // 添加禁止缓存的请求头
                client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true,
                    MustRevalidate = true
                };

                // 添加时间戳避免缓存（也可选用）
                var fullUrl = url + "?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                string content = await client.GetStringAsync(fullUrl);
                ReadmeTextBox.Text = content;
            }
            catch (Exception ex)
            {
                ReadmeTextBox.Text = $"加载失败：{ex.Message}";
            }
        }



    }
}
