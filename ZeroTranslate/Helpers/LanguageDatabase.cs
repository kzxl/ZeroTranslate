using ZeroTranslate.Models;

namespace ZeroTranslate.Helpers;

/// <summary>
/// Static database of supported languages.
/// </summary>
public static class LanguageDatabase
{
    public static readonly IReadOnlyList<Language> Languages = new List<Language>
    {
        Language.Auto,
        new("af", "Afrikaans", "Afrikaans"),
        new("sq", "Albanian", "Shqip"),
        new("ar", "Arabic", "العربية"),
        new("hy", "Armenian", "Հայերեն"),
        new("az", "Azerbaijani", "Azərbaycan"),
        new("eu", "Basque", "Euskara"),
        new("be", "Belarusian", "Беларуская"),
        new("bn", "Bengali", "বাংলা"),
        new("bg", "Bulgarian", "Български"),
        new("ca", "Catalan", "Català"),
        new("zh-CN", "Chinese (Simplified)", "简体中文"),
        new("zh-TW", "Chinese (Traditional)", "繁體中文"),
        new("hr", "Croatian", "Hrvatski"),
        new("cs", "Czech", "Čeština"),
        new("da", "Danish", "Dansk"),
        new("nl", "Dutch", "Nederlands"),
        new("en", "English", "English"),
        new("et", "Estonian", "Eesti"),
        new("fi", "Finnish", "Suomi"),
        new("fr", "French", "Français"),
        new("gl", "Galician", "Galego"),
        new("ka", "Georgian", "ქართული"),
        new("de", "German", "Deutsch"),
        new("el", "Greek", "Ελληνικά"),
        new("gu", "Gujarati", "ગુજરાતી"),
        new("ht", "Haitian Creole", "Kreyòl Ayisyen"),
        new("he", "Hebrew", "עברית"),
        new("hi", "Hindi", "हिन्दी"),
        new("hu", "Hungarian", "Magyar"),
        new("is", "Icelandic", "Íslenska"),
        new("id", "Indonesian", "Bahasa Indonesia"),
        new("ga", "Irish", "Gaeilge"),
        new("it", "Italian", "Italiano"),
        new("ja", "Japanese", "日本語"),
        new("kn", "Kannada", "ಕನ್ನಡ"),
        new("kk", "Kazakh", "Қазақ"),
        new("km", "Khmer", "ភាសាខ្មែរ"),
        new("ko", "Korean", "한국어"),
        new("lo", "Lao", "ລາວ"),
        new("la", "Latin", "Latina"),
        new("lv", "Latvian", "Latviešu"),
        new("lt", "Lithuanian", "Lietuvių"),
        new("mk", "Macedonian", "Македонски"),
        new("ms", "Malay", "Bahasa Melayu"),
        new("ml", "Malayalam", "മലയാളം"),
        new("mt", "Maltese", "Malti"),
        new("mn", "Mongolian", "Монгол"),
        new("my", "Myanmar (Burmese)", "ဗမာ"),
        new("ne", "Nepali", "नेपाली"),
        new("no", "Norwegian", "Norsk"),
        new("fa", "Persian", "فارسی"),
        new("pl", "Polish", "Polski"),
        new("pt", "Portuguese", "Português"),
        new("pa", "Punjabi", "ਪੰਜਾਬੀ"),
        new("ro", "Romanian", "Română"),
        new("ru", "Russian", "Русский"),
        new("sr", "Serbian", "Српски"),
        new("si", "Sinhala", "සිංහල"),
        new("sk", "Slovak", "Slovenčina"),
        new("sl", "Slovenian", "Slovenščina"),
        new("es", "Spanish", "Español"),
        new("sw", "Swahili", "Kiswahili"),
        new("sv", "Swedish", "Svenska"),
        new("ta", "Tamil", "தமிழ்"),
        new("te", "Telugu", "తెలుగు"),
        new("th", "Thai", "ไทย"),
        new("tr", "Turkish", "Türkçe"),
        new("uk", "Ukrainian", "Українська"),
        new("ur", "Urdu", "اردو"),
        new("uz", "Uzbek", "Oʻzbek"),
        new("vi", "Vietnamese", "Tiếng Việt"),
        new("cy", "Welsh", "Cymraeg"),
    };

    /// <summary>
    /// Languages available as source (includes Auto).
    /// </summary>
    public static IReadOnlyList<Language> SourceLanguages => Languages;

    /// <summary>
    /// Languages available as target (excludes Auto). Built once and cached.
    /// </summary>
    public static IReadOnlyList<Language> TargetLanguages { get; } =
        Languages.Where(l => l.Code != "auto").ToList();

    // Code -> Language lookup, built once for O(1) FindByCode.
    private static readonly Dictionary<string, Language> ByCode =
        Languages.ToDictionary(l => l.Code, StringComparer.OrdinalIgnoreCase);

    public static Language? FindByCode(string code) =>
        !string.IsNullOrEmpty(code) && ByCode.TryGetValue(code, out var lang) ? lang : null;
}
