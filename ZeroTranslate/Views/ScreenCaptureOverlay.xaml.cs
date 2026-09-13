using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ZeroTranslate.Views;

/// <summary>
/// Fullscreen overlay for screen region selection (OCR capture).
/// DPI-aware: stores DIP dimensions for pixel-to-DIP conversion.
/// </summary>
public partial class ScreenCaptureOverlay : Window
{
    private System.Windows.Point _startPoint;
    private bool _isDragging;

    /// <summary>The selected region in DIP coordinates.</summary>
    public Rect SelectedRegion { get; private set; } = Rect.Empty;

    /// <summary>The overlay's width in DIP (for DPI conversion).</summary>
    public double ScreenDipWidth { get; private set; }

    /// <summary>The overlay's height in DIP (for DPI conversion).</summary>
    public double ScreenDipHeight { get; private set; }

    public ScreenCaptureOverlay(BitmapSource screenshot)
    {
        InitializeComponent();
        ScreenImage.Source = screenshot;

        // Store DIP dimensions once the window is loaded
        Loaded += (_, _) =>
        {
            ScreenDipWidth = ActualWidth;
            ScreenDipHeight = ActualHeight;
            Log.Debug($"[Overlay] DIP size: {ScreenDipWidth}x{ScreenDipHeight}, Image pixels: {screenshot.PixelWidth}x{screenshot.PixelHeight}");
        };
    }

    private void Canvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(OverlayCanvas);
        _isDragging = true;
        SelectionBorder.Visibility = Visibility.Visible;
        OverlayCanvas.CaptureMouse();
    }

    private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging) return;

        var currentPoint = e.GetPosition(OverlayCanvas);

        var x = Math.Min(_startPoint.X, currentPoint.X);
        var y = Math.Min(_startPoint.Y, currentPoint.Y);
        var width = Math.Abs(currentPoint.X - _startPoint.X);
        var height = Math.Abs(currentPoint.Y - _startPoint.Y);

        Canvas.SetLeft(SelectionBorder, x);
        Canvas.SetTop(SelectionBorder, y);
        SelectionBorder.Width = width;
        SelectionBorder.Height = height;
    }

    private void Canvas_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        OverlayCanvas.ReleaseMouseCapture();

        var endPoint = e.GetPosition(OverlayCanvas);
        var x = Math.Min(_startPoint.X, endPoint.X);
        var y = Math.Min(_startPoint.Y, endPoint.Y);
        var width = Math.Abs(endPoint.X - _startPoint.X);
        var height = Math.Abs(endPoint.Y - _startPoint.Y);

        if (width > 10 && height > 10)
        {
            SelectedRegion = new Rect(x, y, width, height);
            Log.Debug($"[Overlay] Selected region (DIP): ({x:F0},{y:F0} {width:F0}x{height:F0})");
            DialogResult = true;
        }
        else
        {
            SelectedRegion = Rect.Empty;
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            SelectedRegion = Rect.Empty;
            DialogResult = false;
        }
    }
}
