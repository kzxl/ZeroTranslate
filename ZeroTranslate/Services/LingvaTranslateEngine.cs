using System.Net.Http;
using System.Text.Json;
using ZeroTranslate.Helpers;
using ZeroTranslate.Models;

namespace ZeroTranslate.Services;

/// <summary>
/// Lingva Translate — a free, privacy-friendly Google Translate frontend that
/// requires no API key. https://github.com/thedaviddelta/lingva-translate
/// Useful as an additional engine and fallback target.
/// </summary>
public class LingvaTranslateEngine : ITranslationEngine
{
    public string Name => "Lingva";
    public IReadOnlyList<Language> SupportedLanguages => LanguageDatabase.Languages;

    // Public instance. The path format is /api/v1/{source}/{target}/{text}.
    private const string BaseUrl = "https://lingva.ml/api/v1";

    private readonly HttpClient _httpClient = HttpClientProvider.Shared;

    public async Task<TranslationResult> TranslateAsync(
        string text, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new TranslationResult { SourceText = text, TranslatedText = "", IsSuccess = true, EngineName = Name };

        try
        {
            var src = string.IsNullOrEmpty(sourceLang) || sourceLang == "autodetect" ? "auto" : sourceLang;
            // Lingva expects the text as a URL path segment.
            var encoded = Uri.EscapeDataString(text);
            var url = $"{BaseUrl}/{src}/{targetLang}/{encoded}";

            var response = await _httpClient.GetStringAsync(url, ct);
            using var json = JsonDocument.Parse(response);
            var root = json.RootElement;

            var translated = root.TryGetProperty("translation", out var t)
                ? t.GetString() ?? ""
                : "";

            var detectedLang = sourceLang;
            if (root.TryGetProperty("info", out var info) &&
                info.TryGetProperty("detectedSource", out var det))
            {
                detectedLang = det.GetString() ?? sourceLang;
            }

            return new TranslationResult
            {
                SourceText = text,
                TranslatedText = translated,
                SourceLanguageCode = sourceLang,
                TargetLanguageCode = targetLang,
                DetectedLanguageCode = detectedLang,
                EngineName = Name,
                IsSuccess = !string.IsNullOrEmpty(translated)
            };
        }
        catch (Exception ex)
        {
            return new TranslationResult
            {
                SourceText = text, TranslatedText = "", EngineName = Name,
                SourceLanguageCode = sourceLang, TargetLanguageCode = targetLang,
                IsSuccess = false, ErrorMessage = ex.Message
            };
        }
    }

    public async Task<string> DetectLanguageAsync(string text, CancellationToken ct = default)
    {
        var result = await TranslateAsync(text, "auto", "en", ct);
        return result.DetectedLanguageCode;
    }
}
