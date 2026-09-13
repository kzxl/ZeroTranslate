using System.Net.Http;
using System.Text;
using System.Text.Json;
using ZeroTranslate.Models;

namespace ZeroTranslate.Services;

/// <summary>
/// Offline / Private Local LLM translation engine supporting Ollama and OpenAI-compatible local servers (LM Studio, llama.cpp).
/// </summary>
public class LocalLlmTranslateEngine : ITranslationEngine
{
    private readonly HttpClient _httpClient;

    public string Name => "Local LLM (Ollama / LocalAI)";
    public string Endpoint { get; set; } = "http://localhost:11434/api/generate";
    public string ModelName { get; set; } = "qwen2.5:latest";

    public IReadOnlyList<Language> SupportedLanguages { get; } = new List<Language>
    {
        Language.Auto,
        new("vi", "Vietnamese", "Tiếng Việt"),
        new("en", "English", "English"),
        new("ja", "Japanese", "日本語"),
        new("ko", "Korean", "한국어"),
        new("zh", "Chinese", "中文"),
        new("fr", "French", "Français"),
        new("de", "German", "Deutsch"),
        new("ru", "Russian", "Русский"),
        new("es", "Spanish", "Español")
    };

    public LocalLlmTranslateEngine(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<TranslationResult> TranslateAsync(string text, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TranslationResult
            {
                SourceText = text,
                TranslatedText = "",
                SourceLanguageCode = sourceLang,
                TargetLanguageCode = targetLang,
                EngineName = Name,
                IsSuccess = true
            };
        }

        try
        {
            var prompt = $"""
            You are a professional translator. Translate the text from {sourceLang} to {targetLang}.
            Return ONLY the pure translation without any explanation, preamble, quotes, or notes.
            Text to translate:
            {text}
            """;

            var payload = new
            {
                model = ModelName,
                prompt = prompt,
                stream = false
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(Endpoint, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                return new TranslationResult
                {
                    SourceText = text,
                    SourceLanguageCode = sourceLang,
                    TargetLanguageCode = targetLang,
                    EngineName = Name,
                    IsSuccess = false,
                    ErrorMessage = $"Local LLM returned HTTP {response.StatusCode}. Ensure Ollama or local endpoint is running at {Endpoint}."
                };
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            string translated = "";
            if (doc.RootElement.TryGetProperty("response", out var respProp))
            {
                // Ollama native format
                translated = respProp.GetString() ?? "";
            }
            else if (doc.RootElement.TryGetProperty("choices", out var choicesProp) && choicesProp.GetArrayLength() > 0)
            {
                // OpenAI compatible format (LM Studio / vLLM / llama.cpp)
                var first = choicesProp[0];
                if (first.TryGetProperty("message", out var msgProp) && msgProp.TryGetProperty("content", out var contentProp))
                {
                    translated = contentProp.GetString() ?? "";
                }
                else if (first.TryGetProperty("text", out var textProp))
                {
                    translated = textProp.GetString() ?? "";
                }
            }

            translated = translated.Trim();

            return new TranslationResult
            {
                SourceText = text,
                TranslatedText = translated,
                SourceLanguageCode = sourceLang,
                TargetLanguageCode = targetLang,
                DetectedLanguageCode = sourceLang == "auto" ? "en" : sourceLang,
                EngineName = Name,
                IsSuccess = true
            };
        }
        catch (Exception ex)
        {
            return new TranslationResult
            {
                SourceText = text,
                SourceLanguageCode = sourceLang,
                TargetLanguageCode = targetLang,
                EngineName = Name,
                IsSuccess = false,
                ErrorMessage = $"Cannot connect to Local LLM at {Endpoint}: {ex.Message}"
            };
        }
    }

    public Task<string> DetectLanguageAsync(string text, CancellationToken ct = default)
    {
        // Basic heuristic detection for common Asian/Latin scripts
        if (string.IsNullOrWhiteSpace(text)) return Task.FromResult("en");

        foreach (char c in text)
        {
            if (c >= 0x4E00 && c <= 0x9FFF) return Task.FromResult("zh");
            if ((c >= 0x3040 && c <= 0x309F) || (c >= 0x30A0 && c <= 0x30FF)) return Task.FromResult("ja");
            if (c >= 0xAC00 && c <= 0xD7AF) return Task.FromResult("ko");
        }

        return Task.FromResult("en");
    }
}
