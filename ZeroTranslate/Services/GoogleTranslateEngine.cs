using System.Net.Http;
using System.Text.Json;
using System.Web;
using ZeroTranslate.Helpers;
using ZeroTranslate.Models;

namespace ZeroTranslate.Services;

/// <summary>
/// Google Translate using the free unofficial API (translate.googleapis.com).
/// No API key required.
/// </summary>
public class GoogleTranslateEngine : ITranslationEngine
{
    public string Name => "Google Translate";
    public IReadOnlyList<Language> SupportedLanguages => LanguageDatabase.Languages;

    private readonly HttpClient _httpClient = HttpClientProvider.Shared;

    public async Task<TranslationResult> TranslateAsync(
        string text, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TranslationResult
            {
                SourceText = text,
                TranslatedText = "",
                IsSuccess = true,
                EngineName = Name,
                SourceLanguageCode = sourceLang,
                TargetLanguageCode = targetLang
            };
        }

        try
        {
            var encoded = HttpUtility.UrlEncode(text);
            var url = $"https://translate.googleapis.com/translate_a/single" +
                      $"?client=gtx&sl={sourceLang}&tl={targetLang}" +
                      $"&dt=t&dt=at&dj=1&q={encoded}";

            var response = await _httpClient.GetStringAsync(url, ct);
            var json = JsonDocument.Parse(response);
            var root = json.RootElement;

            // Extract translated text from response
            var translated = "";
            var detectedLang = sourceLang;

            if (root.TryGetProperty("sentences", out var sentences))
            {
                foreach (var sentence in sentences.EnumerateArray())
                {
                    if (sentence.TryGetProperty("trans", out var trans))
                    {
                        translated += trans.GetString();
                    }
                }
            }

            if (root.TryGetProperty("src", out var src))
            {
                detectedLang = src.GetString() ?? sourceLang;
            }

            return new TranslationResult
            {
                SourceText = text,
                TranslatedText = translated,
                SourceLanguageCode = sourceLang,
                TargetLanguageCode = targetLang,
                DetectedLanguageCode = detectedLang,
                EngineName = Name,
                IsSuccess = true
            };
        }
        catch (Exception ex)
        {
            return new TranslationResult
            {
                SourceText = text,
                TranslatedText = "",
                SourceLanguageCode = sourceLang,
                TargetLanguageCode = targetLang,
                EngineName = Name,
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<string> DetectLanguageAsync(string text, CancellationToken ct = default)
    {
        var result = await TranslateAsync(text, "auto", "en", ct);
        return result.DetectedLanguageCode;
    }
}
