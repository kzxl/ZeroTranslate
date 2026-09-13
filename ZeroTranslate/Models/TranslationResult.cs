namespace ZeroTranslate.Models;

/// <summary>
/// Holds the result of a translation request.
/// </summary>
public class TranslationResult
{
    public string SourceText { get; init; } = "";
    public string TranslatedText { get; init; } = "";
    public string SourceLanguageCode { get; init; } = "";
    public string TargetLanguageCode { get; init; } = "";
    public string DetectedLanguageCode { get; init; } = "";
    public string EngineName { get; init; } = "";
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
}
