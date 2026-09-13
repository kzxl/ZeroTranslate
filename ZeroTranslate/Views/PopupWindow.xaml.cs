using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using ZeroTranslate.Core.Interfaces;
using ZeroTranslate.Native;
using ZeroTranslate.ViewModels;

namespace ZeroTranslate.Views;

/// <summary>
/// Translation popup that appears near the cursor.
/// </summary>
public partial class PopupWindow : Window
{
    private bool _canClose;
    private readonly DispatcherTimer? _autoCloseTimer;

    public PopupWindow(PopupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Optional auto-close: respect the user's PopupAutoCloseSeconds setting
        // (0 = never auto-close). The timer is paused while the cursor is over
        // the popup so it doesn't vanish mid-read.
        var seconds = ResolveAutoCloseSeconds();
        if (seconds > 0)
        {
            _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
            _autoCloseTimer.Tick += (_, _) =>
            {
                _autoCloseTimer.Stop();
                Close();
            };

            MouseEnter += (_, _) => _autoCloseTimer.Stop();
            MouseLeave += (_, _) => RestartAutoCloseTimer();
        }
    }

    private static int ResolveAutoCloseSeconds()
    {
        try
        {
            return App.Services?.GetService<ISettingsService>()?.Settings.PopupAutoCloseSeconds ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private void RestartAutoCloseTimer()
    {
        if (_autoCloseTimer == null) return;
        _autoCloseTimer.Stop();
        _autoCloseTimer.Start();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Position near cursor
        if (NativeMethods.GetCursorPos(out var point))
        {
            // Ensure popup doesn't go off-screen
            var screen = SystemParameters.WorkArea;
            var left = (double)point.X + 15;
            var top = (double)point.Y + 25; // Cách mũi tên chuột một đoạn xuống dưới để né đoạn text đang bôi đen

            // Nếu popup bị vượt qua mép phải màn hình
            if (left + ActualWidth > screen.Right)
                left = screen.Right - ActualWidth - 10;
                
            // Nếu popup bị vượt qua mép dưới màn hình (đáy), đảo nó lên phía TRÊN con trỏ chuột
            if (top + ActualHeight > screen.Bottom)
                top = point.Y - ActualHeight - 15;

            Left = Math.Max(0, left);
            Top = Math.Max(0, top);
        }

        // Focus for keyboard input (Esc to close)
        Activate();
        Focus();
        
        // Prevent auto-closing immediately due to initial focus glitches
        Task.Delay(200).ContinueWith(_ => _canClose = true);

        // Start the inactivity auto-close countdown.
        RestartAutoCloseTimer();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        // Auto-close when popup loses focus, but only after initial delay
        if (_canClose)
        {
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _autoCloseTimer?.Stop();
        base.OnClosed(e);
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is PopupViewModel vm && !string.IsNullOrEmpty(vm.TranslatedText))
        {
            try { Clipboard.SetText(vm.TranslatedText); } catch { /* ignore */ }

            // Brief inline confirmation so the user knows it worked.
            if (sender is System.Windows.Controls.Button btn)
            {
                var original = btn.Content;
                btn.Content = "✓ Đã chép";
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(1200)
                };
                timer.Tick += (_, _) =>
                {
                    btn.Content = original;
                    timer.Stop();
                };
                timer.Start();
            }
        }
    }
}
