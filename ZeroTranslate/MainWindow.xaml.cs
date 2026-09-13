using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using ZeroTranslate.Core;
using ZeroTranslate.Core.Interfaces;
using ZeroTranslate.Services;
using ZeroTranslate.ViewModels;
using ZeroTranslate.Views;

namespace ZeroTranslate;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    // Resolve services once instead of on every event invocation.
    private readonly ISettingsService _settings = App.Services.GetRequiredService<ISettingsService>();
    private readonly IOcrEngine _ocrEngine = App.Services.GetRequiredService<IOcrEngine>();

    // Debounce timer for instant (as-you-type) translation.
    private readonly DispatcherTimer _instantTimer;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        _instantTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _instantTimer.Tick += async (_, _) =>
        {
            _instantTimer.Stop();
            if (_settings.Settings.InstantTranslate &&
                !ViewModel.IsTranslating &&
                !string.IsNullOrWhiteSpace(ViewModel.SourceText))
            {
                await ViewModel.TranslateAsync();
            }
        };

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Restart the debounce window whenever the source text changes.
        if (e.PropertyName == nameof(MainViewModel.SourceText) && _settings.Settings.InstantTranslate)
        {
            _instantTimer.Stop();
            if (!string.IsNullOrWhiteSpace(ViewModel.SourceText))
                _instantTimer.Start();
        }
    }

    private async void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Return && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            _instantTimer.Stop();
            if (!string.IsNullOrWhiteSpace(ViewModel.SourceText) && !ViewModel.IsTranslating)
                await ViewModel.TranslateAsync();
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_settings.Settings.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _settings.Settings.MinimizeToTray)
        {
            Hide();
            WindowState = WindowState.Normal;
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var registry = App.Services.GetRequiredService<TranslationEngineRegistry>();
        var settingsWindow = new SettingsWindow(_settings, registry, _ocrEngine) { Owner = this };
        if (settingsWindow.ShowDialog() == true)
        {
            var orchestrator = App.Services.GetRequiredService<AppOrchestrator>();
            orchestrator.ReRegisterHotkey();
        }
    }

    private void HistoryToggleButton_Click(object sender, RoutedEventArgs e)
    {
        HistoryPanel.Visibility = HistoryPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryList.SelectedItem is Models.HistoryItem item)
            ViewModel.UseHistoryItemCommand.Execute(item);
    }

    private void ClearHistory_Click(object sender, MouseButtonEventArgs e)
    {
        ViewModel.ClearHistoryCommand.Execute(null);
    }

    private async void OcrButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_ocrEngine.IsAvailable)
        {
            MessageBox.Show("OCR không khả dụng trên hệ thống này.",
                "ZeroTranslate — OCR", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Ẩn main window để chụp màn hình sạch
        var wasVisible = IsVisible;
        if (wasVisible) Hide();

        // Delay nhỏ để window ẩn hoàn toàn
        await Task.Delay(200);

        try
        {
            var captureService = App.Services.GetRequiredService<ScreenCaptureService>();
            var bitmap = captureService.CaptureRegion();

            // Hiện lại main window trước
            if (wasVisible) Show();

            if (bitmap == null) return;

            Log.Debug($"[OCR-MainForm] Captured {bitmap.PixelWidth}x{bitmap.PixelHeight}");

            ViewModel.StatusText = "Đang nhận dạng văn bản (OCR)...";
            var text = await _ocrEngine.RecognizeAsync(bitmap);
            Log.Debug($"[OCR-MainForm] Recognized: '{text}'");

            if (!string.IsNullOrWhiteSpace(text))
            {
                // Đổ text vào main form SourceText → translate 
                ViewModel.SourceText = text.Trim();
                await ViewModel.TranslateAsync();
            }
            else
            {
                ViewModel.StatusText = "OCR không nhận dạng được văn bản nào.";
            }
        }
        catch (Exception ex)
        {
            if (wasVisible && !IsVisible) Show();
            Log.Error($"OCR-MainForm: {ex}");
            ViewModel.StatusText = $"OCR lỗi: {ex.Message}";
        }
    }
}