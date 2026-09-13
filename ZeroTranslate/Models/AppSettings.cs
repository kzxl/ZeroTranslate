using System.Windows.Forms;

namespace ZeroTranslate.Models;

/// <summary>
/// Application settings, persisted to JSON.
/// </summary>
public class AppSettings
{
    // --- Hotkeys ---
    public Keys TranslateHotkey { get; set; } = Keys.Control | Keys.Q;
    public Keys TranslateMainWindowHotkey { get; set; } = Keys.Control | Keys.Enter;
    public Keys OcrHotkey { get; set; } = Keys.Control | Keys.Shift | Keys.Q;

    // --- Languages ---
    public string DefaultSourceLanguage { get; set; } = "auto";
    public string DefaultTargetLanguage { get; set; } = "vi";

    // --- Behavior ---
    public bool StartMinimized { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool ShowPopupOnHotkey { get; set; } = true;
    public bool ShowFloatingIcon { get; set; } = true;
    public bool MouseModeRequiresCtrl { get; set; } = false;
    public int PopupAutoCloseSeconds { get; set; } = 5;

    /// <summary>
    /// Automatically translate in the main window after the user stops typing
    /// (QTranslate-style instant translation). Debounced to avoid spamming the API.
    /// </summary>
    public bool InstantTranslate { get; set; } = true;

    // --- Engine ---
    public string ActiveEngineName { get; set; } = "Google Translate";

    /// <summary>
    /// When the active engine fails, automatically retry with the other
    /// registered engines (QTranslate-style multi-service reliability).
    /// </summary>
    public bool EnableEngineFallback { get; set; } = true;

    // --- OCR ---
    public bool OcrEnabled { get; set; } = true;
    public string OcrLanguage { get; set; } = "en"; // Windows OCR language tag

    // --- Window State ---
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public double WindowWidth { get; set; } = 900;
    public double WindowHeight { get; set; } = 560;
}
