using System.Windows.Media.Imaging;

namespace ZeroTranslate.Core.Interfaces;

/// <summary>
/// Interface for OCR engines. Allows swapping implementations.
/// </summary>
public interface IOcrEngine
{
    /// <summary>
    /// Recognize text from a bitmap image.
    /// </summary>
    Task<string> RecognizeAsync(BitmapSource image, string? languageTag = null, CancellationToken ct = default);

    /// <summary>
    /// List of available OCR language tags on this system.
    /// </summary>
    IReadOnlyList<string> AvailableLanguages { get; }

    /// <summary>
    /// Whether OCR is available on this system.
    /// </summary>
    bool IsAvailable { get; }
}
