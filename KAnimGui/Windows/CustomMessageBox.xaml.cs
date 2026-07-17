using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace KAnimGui.Windows;

public partial class CustomMessageBox : Window
{
    private static FontFamily AppFontFamily =>
        System.Windows.Application.Current?.TryFindResource("AppFontFamily") as FontFamily ??
        new FontFamily("HarmonyOS Sans SC, Microsoft YaHei UI, Segoe UI");

    public CustomMessageBox() : this("操作完成", "提示", PackIconKind.Information)
    {
    }

    public CustomMessageBox(string message, string title, PackIconKind iconKind)
    {
        InitializeComponent();
        Title = title;
        ReadmeTextBox.Text = message;
        IconPack.Kind = iconKind;
        TitleTextBlock.FontFamily = AppFontFamily;
        TitleTextBlock.FontSize = 18;
        TitleTextBlock.FontWeight = FontWeights.Bold;
        TitleTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"));
        TextOptions.SetTextFormattingMode(TitleTextBlock, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(TitleTextBlock, TextRenderingMode.ClearType);
        ReadmeTextBox.FontFamily = AppFontFamily;
        ReadmeTextBox.FontSize = 14;
        ReadmeTextBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555"));
        ReadmeTextBox.Focusable = false;
        ReadmeTextBox.Cursor = Cursors.Arrow;
        ReadmeTextBox.IsHitTestVisible = false;
        TextOptions.SetTextFormattingMode(ReadmeTextBox, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(ReadmeTextBox, TextRenderingMode.ClearType);
        if (iconKind == PackIconKind.CloseCircle)
        {
            ReadmeTextBox.FontWeight = FontWeights.Bold;
        }
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            Close();
        }
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
    }
}
