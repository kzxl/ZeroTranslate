using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ZeroTranslate.Core.Interfaces;
using ZeroTranslate.Models;
using ZeroTranslate.Services;
using ZeroTranslate.ViewModels;
using ZeroTranslate.Views;

namespace ZeroTranslate.Core;

/// <summary>
/// Application orchestrator — manages hotkeys, tray icon, floating icon, and text selection.
/// Extracted from App.xaml.cs to follow Universe Architecture (no God Class).
/// </summary>
public class AppOrchestrator
{
    private readonly ISettingsService _settingsService;
    private readonly IHotkeyService _hotkeyService;
    private readonly IClipboardService _clipboardService;
    private readonly TranslationService _translationService;
    private readonly IOcrEngine _ocrEngine;
    private readonly ScreenCaptureService _screenCaptureService;
    private readonly MainViewModel _mainViewModel;
    private readonly Func<PopupViewModel> _popupViewModelFactory;

    private AppSettings Settings => _settingsService.Settings;

    // --- UI ---
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private FloatingIconWindow? _floatingIcon;
    private TextSelectionMonitor? _textSelectionMonitor;
    private System.Windows.Forms.ToolStripMenuItem? _hotkeyToggle;
    private System.Windows.Forms.ToolStripMenuItem? _mouseToggle;

    public AppOrchestrator(
        ISettingsService settingsService,
        IHotkeyService hotkeyService,
        IClipboardService clipboardService,
        TranslationService translationService,
        IOcrEngine ocrEngine,
        ScreenCaptureService screenCaptureService,
        MainViewModel mainViewModel,
        Func<PopupViewModel> popupViewModelFactory)
    {
        _settingsService = settingsService;
        _hotkeyService = hotkeyService;
        _clipboardService = clipboardService;
        _translationService = translationService;
        _ocrEngine = ocrEngine;
        _screenCaptureService = screenCaptureService;
        _mainViewModel = mainViewModel;
        _popupViewModelFactory = popupViewModelFactory;
    }

    public void Initialize()
    {
        _mainWindow = new MainWindow(_mainViewModel);

        // Hotkey setup
        SetupHotkey();

        // Floating icon
        _floatingIcon = new FloatingIconWindow();
        _floatingIcon.TranslateRequested += OnFloatingIconClicked;

        // Text selection monitor
        if (Settings.ShowFloatingIcon)
            StartTextSelectionMonitor();

        SetupSystemTray();

        if (!Settings.StartMinimized)
            _mainWindow.Show();

        Log.Debug("[ZeroTranslate] Startup complete.");
    }

    // --- Text Selection Monitor ---

    private void StartTextSelectionMonitor()
    {
        _textSelectionMonitor = new TextSelectionMonitor();
        _textSelectionMonitor.PossibleSelection += async (x, y) =>
        {
            Log.Debug($"[FloatingIcon] PossibleSelection at ({x}, {y})");
            if (Settings.MouseModeRequiresCtrl &&
                !System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
            {
                Log.Debug("[FloatingIcon] Skipped — requires Ctrl");
                return;
            }

            _textSelectionMonitor.IsEnabled = false;
            var text = await _clipboardService.GetSelectedTextAsync();
            _textSelectionMonitor.IsEnabled = true;
            Log.Debug($"[FloatingIcon] Clipboard text: '{text?.Substring(0, Math.Min(text?.Length ?? 0, 50))}'");

            if (!string.IsNullOrWhiteSpace(text))
            {
                Application.Current.Dispatcher.Invoke(() => _floatingIcon?.ShowAt(x, y, text.Trim()));
                Log.Debug("[FloatingIcon] ShowAt called");
            }
        };
        _textSelectionMonitor.SelectionCleared += () =>
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (_floatingIcon != null && _floatingIcon.Visibility == Visibility.Visible)
                {
                    Native.NativeMethods.GetCursorPos(out var p);
                    if (p.X >= _floatingIcon.Left - 5 && p.X <= _floatingIcon.Left + _floatingIcon.Width + 5 &&
                        p.Y >= _floatingIcon.Top - 5 && p.Y <= _floatingIcon.Top + _floatingIcon.Height + 5)
                        return;
                }
                _floatingIcon?.HideIcon();
            }, DispatcherPriority.Input);

        _textSelectionMonitor.Start();
    }

    private async void OnFloatingIconClicked(string text)
    {
        try
        {
            _floatingIcon?.HideIcon();
            if (!string.IsNullOrWhiteSpace(text))
                await ShowTranslationPopup(text);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ZeroTranslate] FloatingIcon error: {ex.Message}");
            if (_textSelectionMonitor != null)
                _textSelectionMonitor.IsEnabled = true;
        }
    }

    // --- Hotkey ---

    private void SetupHotkey()
    {
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        // Hotkey 1: Popup
        var hotkeyPopup = Settings.TranslateHotkey;
        bool okPopup = _hotkeyService.RegisterHotkey(hotkeyPopup, 1);
        Log.Debug($"[ZeroTranslate] RegisterHotKey Popup ({hotkeyPopup}) = {okPopup}");

        // Hotkey 2: Main Window
        var hotkeyMain = Settings.TranslateMainWindowHotkey;
        bool okMain = _hotkeyService.RegisterHotkey(hotkeyMain, 2);
        Log.Debug($"[ZeroTranslate] RegisterHotKey Main ({hotkeyMain}) = {okMain}");

        // Hotkey 3: OCR
        var hotkeyOcr = Settings.OcrHotkey;
        bool okOcr = _hotkeyService.RegisterHotkey(hotkeyOcr, 3);
        Log.Debug($"[ZeroTranslate] RegisterHotKey OCR ({hotkeyOcr}) = {okOcr}");

        if (!okPopup)
        {
            MessageBox.Show(
                $"Phím tắt Popup {FormatHotkey(hotkeyPopup)} đã bị ứng dụng khác sử dụng.\n\n" +
                "Vào Cài đặt → Phím tắt để chọn phím tắt khác.",
                "ZeroTranslate — Phím tắt",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public void ReRegisterHotkey()
    {
        _hotkeyService.UnregisterHotkey(1);
        _hotkeyService.UnregisterHotkey(2);
        _hotkeyService.UnregisterHotkey(3);

        _hotkeyService.RegisterHotkey(Settings.TranslateHotkey, 1);
        _hotkeyService.RegisterHotkey(Settings.TranslateMainWindowHotkey, 2);
        _hotkeyService.RegisterHotkey(Settings.OcrHotkey, 3);

        // Apply engine-fallback preference live (no restart needed).
        _translationService.EnableFallback = Settings.EnableEngineFallback;

        if (_trayIcon != null)
            _trayIcon.Text = $"ZeroTranslate — Dịch nhanh ({FormatHotkey(Settings.TranslateHotkey)})";

        // Update floating icon
        if (Settings.ShowFloatingIcon && _textSelectionMonitor == null)
            StartTextSelectionMonitor();
        else if (!Settings.ShowFloatingIcon && _textSelectionMonitor != null)
        {
            _textSelectionMonitor.Dispose();
            _textSelectionMonitor = null;
            _floatingIcon?.HideIcon();
        }
    }

    private async void OnHotkeyPressed(int hotkeyId)
    {
        Log.Debug($"[ZeroTranslate] Hotkey pressed: ID={hotkeyId}");
        try
        {
            if (_textSelectionMonitor != null)
                _textSelectionMonitor.IsEnabled = false;
            _floatingIcon?.HideIcon();

            if (hotkeyId == 3) // OCR
            {
                if (_textSelectionMonitor != null)
                    _textSelectionMonitor.IsEnabled = true;
                await PerformOcrTranslation();
                return;
            }

            var text = await _clipboardService.GetSelectedTextAsync() ?? "";
            Log.Debug($"[Hotkey] GetSelectedText result: '{text?.Substring(0, Math.Min(text?.Length ?? 0, 50))}'");

            if (_textSelectionMonitor != null)
                _textSelectionMonitor.IsEnabled = true;

            if (hotkeyId == 1) // Popup (Ctrl+Q)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    Log.Debug("[Hotkey] Showing popup for Ctrl+Q...");
                    await ShowTranslationPopup(text);
                }
                else
                {
                    Log.Debug("[Hotkey] No text selected for popup");
                }
            }
            else if (hotkeyId == 2) // Main Window (Ctrl+Enter)
            {
                ShowMainWindow();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    _mainViewModel.SourceText = text.Trim();
                    await _mainViewModel.TranslateAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"[ZeroTranslate] Hotkey error: {ex.Message}");
            if (_textSelectionMonitor != null)
                _textSelectionMonitor.IsEnabled = true;
        }
    }

    // --- OCR ---

    private async Task PerformOcrTranslation()
    {
        Log.Debug("[OCR] PerformOcrTranslation called (hotkey)");

        if (!_ocrEngine.IsAvailable)
        {
            Log.Debug("[OCR] Engine not available!");
            MessageBox.Show(
                "OCR không khả dụng trên hệ thống này.\nCần Windows 10 phiên bản 2004 trở lên.",
                "ZeroTranslate — OCR", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Log.Debug($"[OCR] Engine available. Languages: {string.Join(", ", _ocrEngine.AvailableLanguages)}");

        try
        {
            // Step 1: Hide main window to get clean screenshot
            bool wasVisible = false;
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (_mainWindow != null && _mainWindow.IsVisible)
                {
                    wasVisible = true;
                    _mainWindow.Hide();
                }
            });

            // Wait for window to fully hide
            await Task.Delay(250);

            // Step 2: Capture screen region (shows overlay for user to select)
            Log.Debug("[OCR] Step 1: Capturing screen region...");
            BitmapSource? bitmap = null;
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    bitmap = _screenCaptureService.CaptureRegion();
                    Log.Debug($"[OCR] CaptureRegion returned: {(bitmap != null ? $"{bitmap.PixelWidth}x{bitmap.PixelHeight}" : "null")}");
                }
                catch (Exception ex)
                {
                    Log.Debug($"[OCR] CaptureRegion exception: {ex}");
                }
            });

            // Restore main window
            if (wasVisible)
            {
                await Application.Current.Dispatcher.InvokeAsync(() => _mainWindow?.Show());
            }

            if (bitmap == null)
            {
                Log.Debug("[OCR] Bitmap is null — user cancelled or capture failed");
                return;
            }

            // Step 3: Show the popup immediately in a busy state so the user sees
            // feedback while OCR runs, then run recognition and translate in-place.
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                PopupWindow? popup = null;
                PopupViewModel? vm = null;
                try
                {
                    vm = _popupViewModelFactory();
                    vm.SetBusy("Đang nhận dạng văn bản (OCR)...");
                    popup = new PopupWindow(vm);
                    popup.Show();
                    popup.Activate();

                    var text = await _ocrEngine.RecognizeAsync(bitmap);
                    Log.Debug($"[OCR] OCR result: '{text}'");

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        await vm.TranslateAsync(text.Trim(), Settings.DefaultTargetLanguage);
                    }
                    else
                    {
                        popup.Close();
                        MessageBox.Show(
                            "Không nhận dạng được văn bản nào.\nThử chọn vùng lớn hơn hoặc vùng có chữ rõ ràng.",
                            "ZeroTranslate — OCR", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"OCR popup flow: {ex}");
                    popup?.Close();
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error($"OCR: {ex}");
        }
    }

    // --- Popup ---

    private async Task ShowTranslationPopup(string text)
    {
        Log.Debug($"[Popup] ShowTranslationPopup called: '{text.Substring(0, Math.Min(text.Length, 50))}'");
        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                var vm = _popupViewModelFactory();
                var popup = new PopupWindow(vm);
                popup.Show();
                popup.Activate();
                Log.Debug($"[Popup] Window shown at ({popup.Left}, {popup.Top})");
                await vm.TranslateAsync(text, Settings.DefaultTargetLanguage);
                Log.Debug($"[Popup] Translation done: '{vm.TranslatedText?.Substring(0, Math.Min(vm.TranslatedText?.Length ?? 0, 50))}'");
            }
            catch (Exception ex)
            {
                Log.Debug($"[Popup] Error: {ex}");
            }
        });
    }

    // --- System Tray ---

    private void SetupSystemTray()
    {
        System.Drawing.Icon appIcon;
        try
        {
            var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "icon.ico");
            appIcon = System.IO.File.Exists(iconPath)
                ? new System.Drawing.Icon(iconPath)
                : System.Drawing.SystemIcons.Application;
        }
        catch { appIcon = System.Drawing.SystemIcons.Application; }

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = $"ZeroTranslate — Dịch nhanh ({FormatHotkey(Settings.TranslateHotkey)})",
            Icon = appIcon,
            Visible = true,
            ContextMenuStrip = CreateTrayMenu()
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private System.Windows.Forms.ContextMenuStrip CreateTrayMenu()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();

        var titleItem = new System.Windows.Forms.ToolStripMenuItem("ZeroTranslate");
        titleItem.Font = new System.Drawing.Font(titleItem.Font.FontFamily, titleItem.Font.Size + 1, System.Drawing.FontStyle.Bold);
        titleItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(titleItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        var showItem = new System.Windows.Forms.ToolStripMenuItem("Hiện cửa sổ chính");
        showItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(showItem);

        var ocrItem = new System.Windows.Forms.ToolStripMenuItem($"Chụp & dịch (OCR) — {FormatHotkey(Settings.OcrHotkey)}");
        ocrItem.Click += async (_, _) => await PerformOcrTranslation();
        ocrItem.Enabled = _ocrEngine.IsAvailable;
        menu.Items.Add(ocrItem);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        var settingsItem = new System.Windows.Forms.ToolStripMenuItem("Cài đặt");
        settingsItem.Click += (_, _) =>
        {
            ShowMainWindow();
            if (_mainWindow != null)
            {
                var sw = new SettingsWindow(_settingsService, _translationService.Registry, _ocrEngine) { Owner = _mainWindow };
                if (sw.ShowDialog() == true) ReRegisterHotkey();
            }
        };
        menu.Items.Add(settingsItem);

        var aboutItem = new System.Windows.Forms.ToolStripMenuItem("Về ZeroTranslate");
        aboutItem.Click += (_, _) =>
        {
            MessageBox.Show(
                "ZeroTranslate v1.1\n\n" +
                "Phần mềm dịch thuật nhanh cho Windows.\n" +
                "Thay thế QTranslate — hiện đại, nhẹ nhàng.\n\n" +
                $"Engine: {_translationService.Registry.ActiveEngineName}\n" +
                $"OCR: {(_ocrEngine.IsAvailable ? "Có" : "Không")}\n" +
                $"Hotkey: {FormatHotkey(Settings.TranslateHotkey)}\n" +
                $".NET {Environment.Version}",
                "Về ZeroTranslate", MessageBoxButton.OK, MessageBoxImage.Information);
        };
        menu.Items.Add(aboutItem);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        _hotkeyToggle = new System.Windows.Forms.ToolStripMenuItem("Bật phím tắt toàn cục");
        _hotkeyToggle.Checked = true;
        _hotkeyToggle.CheckOnClick = true;
        _hotkeyToggle.CheckedChanged += (_, _) =>
        {
            if (_hotkeyToggle.Checked)
                _hotkeyService.RegisterHotkey(Settings.TranslateHotkey);
            else
                _hotkeyService.UnregisterHotkey();
        };
        menu.Items.Add(_hotkeyToggle);

        _mouseToggle = new System.Windows.Forms.ToolStripMenuItem("Chế độ chuột (floating icon)");
        _mouseToggle.Checked = Settings.ShowFloatingIcon;
        _mouseToggle.CheckOnClick = true;
        _mouseToggle.CheckedChanged += (_, _) =>
        {
            Settings.ShowFloatingIcon = _mouseToggle.Checked;
            if (_mouseToggle.Checked && _textSelectionMonitor == null)
                StartTextSelectionMonitor();
            else if (!_mouseToggle.Checked)
            {
                _textSelectionMonitor?.Dispose();
                _textSelectionMonitor = null;
                _floatingIcon?.HideIcon();
            }
        };
        menu.Items.Add(_mouseToggle);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Thoát");
        exitItem.Click += (_, _) => ExitApplication();
        menu.Items.Add(exitItem);

        return menu;
    }

    public void ShowMainWindow()
    {
        if (_mainWindow != null)
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }
    }

    public void Shutdown()
    {
        _textSelectionMonitor?.Dispose();
        _hotkeyService.Dispose();
        _floatingIcon?.Close();
        _trayIcon?.Dispose();
        _mainWindow?.Close();
        _settingsService.Save();
    }

    private void ExitApplication()
    {
        Shutdown();
        Application.Current.Shutdown();
    }

    public static string FormatHotkey(System.Windows.Forms.Keys hotkey)
    {
        var parts = new List<string>();
        if (hotkey.HasFlag(System.Windows.Forms.Keys.Control)) parts.Add("Ctrl");
        if (hotkey.HasFlag(System.Windows.Forms.Keys.Alt)) parts.Add("Alt");
        if (hotkey.HasFlag(System.Windows.Forms.Keys.Shift)) parts.Add("Shift");
        var key = hotkey & System.Windows.Forms.Keys.KeyCode;
        if (key != System.Windows.Forms.Keys.None) parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}
