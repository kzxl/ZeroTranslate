using System.Net.Http;
using System.Text.Json;
using System.Web;
using ZeroTranslate.Helpers;
using ZeroTranslate.Models;

namespace ZeroTranslate.Services;

/// <summary>
/// MyMemory Translation API — free, no API key required.
/// https://mymemory.translated.net/doc/spec.php
/// </summary>
public class MyMemoryTranslateEngine : ITranslationEngine
{
    public string Name => "MyMemory";
    public IReadOnlyList<Language> SupportedLanguages => LanguageDatabase.Languages;

    private readonly HttpClient _httpClient = HttpClientProvider.Shared;

    public async Task<TranslationResult> TranslateAsync(
        string text, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new TranslationResult { SourceText = text, TranslatedText = "", IsSuccess = true, EngineName = Name };

        try
        {
            var src = sourceLang == "auto" ? "autodetect" : sourceLang;
            var langPair = $"{src}|{targetLang}";
            var encoded = HttpUtility.UrlEncode(text);
            var url = $"https://api.mymemory.translated.net/get?q={encoded}&langpair={langPair}";

            var response = await _httpClient.GetStringAsync(url, ct);
            var json = JsonDocument.Parse(response);
            var root = json.RootElement;

            var translated = "";
            var detectedLang = sourceLang;

            if (root.TryGetProperty("responseData", out var data))
            {
                if (data.TryGetProperty("translatedText", out var trans))
                    translated = trans.GetString() ?? "";

                if (data.TryGetProperty("detectedLanguage", out var detected))
                    detectedLang = detected.GetString() ?? sourceLang;
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
                SourceText = text, TranslatedText = "", EngineName = Name,
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
