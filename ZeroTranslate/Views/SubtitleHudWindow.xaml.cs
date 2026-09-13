using System.Windows;
using System.Windows.Input;

namespace ZeroTranslate.Views;

public partial class SubtitleHudWindow : Window
{
    public SubtitleHudWindow()
    {
        InitializeComponent();
        PositionAtBottomCenter();
    }

    private void PositionAtBottomCenter()
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        Left = (screenWidth - Width) / 2;
        Top = screenHeight - Height - 60;
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    public void UpdateSubtitle(string sourceText, string translatedText)
    {
        TxtSourceSubtitle.Text = sourceText;
        TxtTranslatedSubtitle.Text = translatedText;
        if (!IsVisible)
        {
            Show();
        }
    }

    public void ClearSubtitle()
    {
        TxtSourceSubtitle.Text = "";
        TxtTranslatedSubtitle.Text = "";
    }
}
