namespace ZeroTranslate.Core.Interfaces;

/// <summary>
/// Text-to-speech service. Speaks a piece of text, optionally hinting the
/// language so the right voice is chosen.
/// </summary>
public interface ITtsService
{
    /// <summary>Whether TTS is available on this system.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Speak the given text. <paramref name="languageTag"/> is a BCP-47 tag
    /// (e.g. "en", "vi", "ja"); when null the default voice is used.
    /// </summary>
    Task SpeakAsync(string text, string? languageTag = null);

    /// <summary>Stop any current playback.</summary>
    void Stop();
}
