using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ZeroTranslate.Views;

/// <summary>
/// Small floating translate icon that appears near selected text.
/// Clicking it triggers translation popup.
/// </summary>
public partial class FloatingIconWindow : Window
{
    public event Action<string>? TranslateRequested;
    
    private string _selectedText = "";
    private readonly DispatcherTimer _autoHideTimer;

    public FloatingIconWindow()
    {
        InitializeComponent();

        _autoHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _autoHideTimer.Tick += (_, _) => HideWithAnimation();
    }

    public void ShowAt(double x, double y, string selectedText)
    {
        _selectedText = selectedText;

        // Position the icon near the cursor (offset slightly up-right)
        var screen = SystemParameters.WorkArea;
        Left = Math.Min(x + 8, screen.Right - 40);
        Top = Math.Max(y - 44, 0);

        // Show with fade-in
        Opacity = 0;
        Show();

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
        BeginAnimation(OpacityProperty, fadeIn);

        // Auto-hide after 5 seconds
        _autoHideTimer.Stop();
        _autoHideTimer.Start();
    }

    private void HideWithAnimation()
    {
        _autoHideTimer.Stop();
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
        fadeOut.Completed += (_, _) => Hide();
        BeginAnimation(OpacityProperty, fadeOut);
    }

    private void Icon_Click(object sender, MouseButtonEventArgs e)
    {
        _autoHideTimer.Stop();
        Hide();
        
        if (!string.IsNullOrWhiteSpace(_selectedText))
        {
            TranslateRequested?.Invoke(_selectedText);
        }
    }

    private void Icon_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _autoHideTimer.Stop();
        IconBorder.Background = new SolidColorBrush(Color.FromRgb(0x9D, 0x4E, 0xDD));
        
        var scale = new DoubleAnimation(1.15, TimeSpan.FromMilliseconds(100));
        IconScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        IconScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
    }

    private void Icon_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _autoHideTimer.Start();
        IconBorder.Background = new SolidColorBrush(Color.FromRgb(0x6C, 0x63, 0xFF));
        
        var scale = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(100));
        IconScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        IconScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        // Don't auto-hide on deactivated since we're ShowActivated=False
    }

    public void HideIcon()
    {
        _autoHideTimer.Stop();
        Hide();
    }
}
