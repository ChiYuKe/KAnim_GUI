using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace KAnimGui.Windows;

public partial class CustomMessageBox : Window
{
    private readonly MessageBoxButton buttons;

    private static FontFamily AppFontFamily =>
        System.Windows.Application.Current?.TryFindResource("AppFontFamily") as FontFamily ??
        new FontFamily("HarmonyOS Sans SC, Microsoft YaHei UI, Segoe UI");

    public CustomMessageBox() : this("操作完成", "提示", PackIconKind.Information, MessageBoxButton.OK)
    {
    }

    public CustomMessageBox(
        string message,
        string title,
        PackIconKind iconKind,
        MessageBoxButton buttons = MessageBoxButton.OK)
    {
        InitializeComponent();
        this.buttons = buttons;
        Title = title;
        MessageTextBlock.Text = message;
        IconPack.Kind = iconKind;
        TitleTextBlock.FontFamily = AppFontFamily;
        TextOptions.SetTextFormattingMode(TitleTextBlock, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(TitleTextBlock, TextRenderingMode.ClearType);
        MessageTextBlock.FontFamily = AppFontFamily;
        TextOptions.SetTextFormattingMode(MessageTextBlock, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(MessageTextBlock, TextRenderingMode.ClearType);
        if (iconKind == PackIconKind.CloseCircle)
        {
            MessageTextBlock.FontWeight = FontWeights.SemiBold;
        }

        ConfigureButtons(buttons);
    }

    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    public static MessageBoxResult Show(
        Window? owner,
        string message,
        string title,
        PackIconKind iconKind,
        MessageBoxButton buttons = MessageBoxButton.OK)
    {
        var dialog = new CustomMessageBox(message, title, iconKind, buttons);
        if (owner is { IsLoaded: true, IsVisible: true })
        {
            dialog.Owner = owner;
        }

        dialog.ShowDialog();
        return dialog.Result;
    }

    private void ConfigureButtons(MessageBoxButton buttonMode)
    {
        if (buttonMode == MessageBoxButton.YesNo)
        {
            SecondaryButton.Content = "否";
            SecondaryButton.Visibility = Visibility.Visible;
            ConfirmButton.Content = "是";
            return;
        }

        if (buttonMode == MessageBoxButton.OKCancel)
        {
            SecondaryButton.Content = "取消";
            SecondaryButton.Visibility = Visibility.Visible;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Result = buttons == MessageBoxButton.YesNo
            ? MessageBoxResult.No
            : buttons == MessageBoxButton.OKCancel
                ? MessageBoxResult.Cancel
                : MessageBoxResult.OK;
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        Result = buttons == MessageBoxButton.YesNo ? MessageBoxResult.Yes : MessageBoxResult.OK;
        Close();
    }

    private void SecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        Result = buttons == MessageBoxButton.YesNo ? MessageBoxResult.No : MessageBoxResult.Cancel;
        Close();
    }
}
