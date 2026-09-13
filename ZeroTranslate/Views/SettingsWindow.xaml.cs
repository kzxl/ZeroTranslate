using System.Windows;
using System.Windows.Input;
using ZeroTranslate.Core;
using ZeroTranslate.Core.Interfaces;
using ZeroTranslate.Helpers;
using ZeroTranslate.Services;

namespace ZeroTranslate.Views;

public partial class SettingsWindow : Window
{
    private readonly ISettingsService _settingsService;
    private readonly TranslationEngineRegistry _engineRegistry;
    private readonly IOcrEngine _ocrEngine;
    private System.Windows.Forms.Keys _capturedHotkey;
    private bool _isRecordingHotkey;

    public SettingsWindow(ISettingsService settingsService, TranslationEngineRegistry engineRegistry, IOcrEngine ocrEngine)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _engineRegistry = engineRegistry;
        _ocrEngine = ocrEngine;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Settings;

        // Hotkey
        _capturedHotkey = settings.TranslateHotkey;
        txtHotkey.Text = AppOrchestrator.FormatHotkey(_capturedHotkey);

        // Language
        cboTargetLang.ItemsSource = LanguageDatabase.TargetLanguages;
        cboTargetLang.SelectedItem = LanguageDatabase.FindByCode(settings.DefaultTargetLanguage)
                                     ?? LanguageDatabase.FindByCode("vi");

        // Behavior
        chkMinimizeToTray.IsChecked = settings.MinimizeToTray;
        chkStartMinimized.IsChecked = settings.StartMinimized;
        chkShowFloatingIcon.IsChecked = settings.ShowFloatingIcon;
        chkInstantTranslate.IsChecked = settings.InstantTranslate;

        // Engine
        cboEngine.ItemsSource = _engineRegistry.AvailableEngines;
        cboEngine.SelectedItem = _engineRegistry.ActiveEngineName;
        chkEngineFallback.IsChecked = settings.EnableEngineFallback;
        txtLocalLlmEndpoint.Text = string.IsNullOrWhiteSpace(settings.LocalLlmEndpoint)
            ? "http://localhost:11434/api/generate"
            : settings.LocalLlmEndpoint;
        txtLocalLlmModel.Text = string.IsNullOrWhiteSpace(settings.LocalLlmModel)
            ? "qwen2.5:latest"
            : settings.LocalLlmModel;

        // OCR
        chkOcrEnabled.IsChecked = settings.OcrEnabled;
        cboOcrLang.ItemsSource = _ocrEngine.AvailableLanguages;
        cboOcrLang.SelectedItem = _ocrEngine.AvailableLanguages.Contains(settings.OcrLanguage)
            ? settings.OcrLanguage
            : _ocrEngine.AvailableLanguages.FirstOrDefault();
        txtOcrHotkey.Text = AppOrchestrator.FormatHotkey(settings.OcrHotkey);

        // Reflect the real autostart state from the registry, not just the saved flag.
        chkStartWithWindows.IsChecked = AutostartService.IsEnabled();
        txtPopupDelay.Text = settings.PopupAutoCloseSeconds.ToString();
    }

    // --- Hotkey Recorder ---
    private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
    {
        _isRecordingHotkey = true;
        txtHotkey.Text = "Nhấn tổ hợp phím...";
        txtHotkey.Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x81, 0x8C, 0xF8));
        txtHotkeyHint.Text = "Đang ghi...";
    }

    private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _isRecordingHotkey = false;
        txtHotkey.Text = AppOrchestrator.FormatHotkey(_capturedHotkey);
        txtHotkey.Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0xF1, 0xF5, 0xF9));
        txtHotkeyHint.Text = "Click để đổi";
    }

    private void HotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_isRecordingHotkey) return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        var wpfKey = KeyInterop.VirtualKeyFromKey(key);
        var formsKey = (System.Windows.Forms.Keys)wpfKey;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            formsKey |= System.Windows.Forms.Keys.Control;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            formsKey |= System.Windows.Forms.Keys.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            formsKey |= System.Windows.Forms.Keys.Shift;

        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            txtHotkey.Text = "Cần ít nhất Ctrl hoặc Alt";
            return;
        }

        _capturedHotkey = formsKey;
        txtHotkey.Text = AppOrchestrator.FormatHotkey(_capturedHotkey);
        txtHotkey.Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x22, 0xC5, 0x5E));
        txtHotkeyHint.Text = "✓ Đã ghi";
        _isRecordingHotkey = false;
        Keyboard.ClearFocus();
    }

    // --- Save / Cancel ---
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Settings;

        settings.TranslateHotkey = _capturedHotkey;

        if (cboTargetLang.SelectedItem is Models.Language lang)
            settings.DefaultTargetLanguage = lang.Code;

        settings.MinimizeToTray = chkMinimizeToTray.IsChecked ?? true;
        settings.StartMinimized = chkStartMinimized.IsChecked ?? false;
        settings.ShowFloatingIcon = chkShowFloatingIcon.IsChecked ?? true;
        settings.StartWithWindows = chkStartWithWindows.IsChecked ?? false;
        settings.InstantTranslate = chkInstantTranslate.IsChecked ?? true;
        settings.OcrEnabled = chkOcrEnabled.IsChecked ?? true;

        // Apply autostart change to the Windows Run registry key.
        AutostartService.SetEnabled(settings.StartWithWindows);

        if (cboOcrLang.SelectedItem is string ocrLang)
            settings.OcrLanguage = ocrLang;

        if (int.TryParse(txtPopupDelay.Text, out var delay) && delay >= 0)
            settings.PopupAutoCloseSeconds = delay;
        if (cboEngine.SelectedItem is string engineName)
            _engineRegistry.ActiveEngineName = engineName;

        settings.EnableEngineFallback = chkEngineFallback.IsChecked ?? true;

        settings.LocalLlmEndpoint = txtLocalLlmEndpoint.Text.Trim();
        settings.LocalLlmModel = txtLocalLlmModel.Text.Trim();

        // Update active Local LLM instance in registry if present
        if (_engineRegistry.GetAllEngines().OfType<LocalLlmTranslateEngine>().FirstOrDefault() is { } llmEngine)
        {
            llmEngine.Endpoint = settings.LocalLlmEndpoint;
            llmEngine.ModelName = settings.LocalLlmModel;
        }

        _settingsService.Save();
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
