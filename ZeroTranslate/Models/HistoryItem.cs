namespace ZeroTranslate.Models;

/// <summary>
/// A single past translation, persisted for the history panel.
/// </summary>
public class HistoryItem
{
    public string SourceText { get; set; } = "";
    public string TranslatedText { get; set; } = "";
    public string SourceLanguageCode { get; set; } = "";
    public string TargetLanguageCode { get; set; } = "";
    public string EngineName { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>Short preview of the source text for list display.</summary>
    public string Preview =>
        SourceText.Length <= 60 ? SourceText : SourceText.Substring(0, 60) + "…";
}
