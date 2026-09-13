using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using WinForms = System.Windows.Forms;

namespace ZeroTranslate.Services;

/// <summary>
/// Service for capturing a screen region selected by the user.
/// Shows a fullscreen overlay where user draws a rectangle.
/// DPI-aware: uses physical pixel dimensions for accurate capture.
/// </summary>
public class ScreenCaptureService
{
    /// <summary>
    /// Capture a region of the screen selected by the user.
    /// Returns null if the user cancels.
    /// </summary>
    public BitmapSource? CaptureRegion()
    {
        // First, capture the entire virtual screen (all monitors)
        var screenBitmap = CaptureFullScreen();
        if (screenBitmap == null) return null;

        // Show overlay for user to select region
        var overlay = new Views.ScreenCaptureOverlay(screenBitmap);
        var result = overlay.ShowDialog();

        if (result != true || overlay.SelectedRegion == Rect.Empty)
            return null;

        // Crop the selected region
        var region = overlay.SelectedRegion;

        // The overlay is displayed in DIP coordinates, but the bitmap is in physical pixels.
        // We need to convert the selection (DIP) to pixel coordinates.
        // Use PresentationSource DPI but since overlay is closed, use stored DPI.
        double scaleX = screenBitmap.PixelWidth / overlay.ScreenDipWidth;
        double scaleY = screenBitmap.PixelHeight / overlay.ScreenDipHeight;

        var pixelX = (int)(region.X * scaleX);
        var pixelY = (int)(region.Y * scaleY);
        var pixelWidth = (int)(region.Width * scaleX);
        var pixelHeight = (int)(region.Height * scaleY);

        // Clamp
        pixelX = Math.Max(0, pixelX);
        pixelY = Math.Max(0, pixelY);
        pixelWidth = Math.Min(pixelWidth, screenBitmap.PixelWidth - pixelX);
        pixelHeight = Math.Min(pixelHeight, screenBitmap.PixelHeight - pixelY);

        if (pixelWidth <= 0 || pixelHeight <= 0)
            return null;

        Log.Debug($"[Capture] Region DIP: ({region.X:F0},{region.Y:F0} {region.Width:F0}x{region.Height:F0}) → Pixel: ({pixelX},{pixelY} {pixelWidth}x{pixelHeight})");

        return new CroppedBitmap(screenBitmap, new Int32Rect(pixelX, pixelY, pixelWidth, pixelHeight));
    }

    /// <summary>
    /// Capture the entire virtual screen using physical pixel dimensions.
    /// </summary>
    private static BitmapSource? CaptureFullScreen()
    {
        try
        {
            // Use System.Windows.Forms for PHYSICAL pixel dimensions (DPI-aware)
            var virtualScreen = WinForms.SystemInformation.VirtualScreen;
            Log.Debug($"[Capture] VirtualScreen: {virtualScreen.X},{virtualScreen.Y} {virtualScreen.Width}x{virtualScreen.Height} (physical pixels)");

            using var bmp = new Bitmap(virtualScreen.Width, virtualScreen.Height);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(virtualScreen.Left, virtualScreen.Top, 0, 0,
                new System.Drawing.Size(virtualScreen.Width, virtualScreen.Height));

            // Convert System.Drawing.Bitmap to WPF BitmapSource
            var handle = bmp.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    handle,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                Native.NativeMethods.DeleteObject(handle);
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"[Capture] CaptureFullScreen error: {ex.Message}");
            return null;
        }
    }
}
