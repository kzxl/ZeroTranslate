using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using ZeroTranslate.Core.Interfaces;

namespace ZeroTranslate.Services;

/// <summary>
/// OCR engine using Windows.Media.Ocr (built-in Windows 10+ API).
/// No external dependencies required.
/// </summary>
public class WindowsOcrEngine : IOcrEngine
{
    // Cache OcrEngine instances per language tag. Creating one is relatively
    // expensive and the instances are reusable across recognitions.
    private readonly ConcurrentDictionary<string, OcrEngine?> _engineCache = new();

    public bool IsAvailable => OcrEngine.AvailableRecognizerLanguages.Count > 0;

    public IReadOnlyList<string> AvailableLanguages =>
        OcrEngine.AvailableRecognizerLanguages
            .Select(l => l.LanguageTag)
            .ToList();

    public async Task<string> RecognizeAsync(BitmapSource image, string? languageTag = null, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            var engine = GetEngine(languageTag);
            if (engine == null)
            {
                Debug.WriteLine("[OCR] No OCR engine available.");
                return "";
            }

            var softwareBitmap = ConvertToSoftwareBitmap(image);
            ct.ThrowIfCancellationRequested();

            var result = await engine.RecognizeAsync(softwareBitmap).AsTask(ct);
            return result.Text;
        }
        catch (OperationCanceledException)
        {
            return "";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OCR] Error: {ex.Message}");
            return "";
        }
    }

    private OcrEngine? GetEngine(string? languageTag)
    {
        // Key the cache by the requested tag (empty string = user profile default).
        var key = languageTag ?? "";
        return _engineCache.GetOrAdd(key, static k =>
        {
            if (!string.IsNullOrEmpty(k))
            {
                var lang = new Windows.Globalization.Language(k);
                if (OcrEngine.IsLanguageSupported(lang))
                {
                    var fromLang = OcrEngine.TryCreateFromLanguage(lang);
                    if (fromLang != null)
                        return fromLang;
                }
            }
            return OcrEngine.TryCreateFromUserProfileLanguages();
        });
    }

    /// <summary>
    /// Converts a WPF BitmapSource straight to a Bgra8 SoftwareBitmap via its
    /// pixel buffer. This avoids the previous PNG encode-to-memory then
    /// decode-back round-trip, cutting allocations and time noticeably.
    /// </summary>
    private static SoftwareBitmap ConvertToSoftwareBitmap(BitmapSource source)
    {
        // Ensure the pixel format is BGRA8 (what SoftwareBitmap expects below).
        BitmapSource bgra = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        int width = bgra.PixelWidth;
        int height = bgra.PixelHeight;
        int stride = width * 4;
        var pixels = new byte[stride * height];
        bgra.CopyPixels(pixels, stride, 0);

        return SoftwareBitmap.CreateCopyFromBuffer(
            pixels.AsBuffer(),
            BitmapPixelFormat.Bgra8,
            width, height,
            BitmapAlphaMode.Premultiplied);
    }
}
