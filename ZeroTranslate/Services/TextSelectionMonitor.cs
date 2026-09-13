using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using ZeroTranslate.Helpers;
using ZeroTranslate.Native;

namespace ZeroTranslate.Services;

/// <summary>
/// Monitors mouse activity to detect text selection (drag-selection or double-click).
/// When text selection is detected, fires PossibleSelection to show floating icon.
/// </summary>
public class TextSelectionMonitor : IDisposable
{
    /// <summary>Fired when user potentially selected text (mouse up after drag or double-click).</summary>
    public event Action<int, int>? PossibleSelection; // cursorX, cursorY

    /// <summary>Fired when selection is likely cleared (single click without drag).</summary>
    public event Action? SelectionCleared;

    private IntPtr _mouseHookId = IntPtr.Zero;
    private NativeMethods.LowLevelMouseProc? _mouseProc;
    private bool _isMouseDown;
    private NativeMethods.POINT _mouseDownPoint;
    private NativeMethods.POINT _lastUpPoint;
    private long _lastUpTimestamp;
    private readonly DispatcherTimer _debounceTimer;
    private bool _disposed;

    public bool IsEnabled { get; set; } = true;

    private const int MinDragDistance = 8; // pixels — drag threshold

    public TextSelectionMonitor()
    {
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _debounceTimer.Tick += OnDebounceTimerTick;
    }

    public void Start()
    {
        if (_mouseHookId != IntPtr.Zero) return;

        _mouseProc = MouseHookCallback;
        _mouseHookId = SetMouseHook(_mouseProc);
        Log.Debug($"TextSelectionMonitor started. Hook={_mouseHookId}");
    }

    public void Stop()
    {
        if (_mouseHookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHookId);
            _mouseHookId = IntPtr.Zero;
        }
    }

    private IntPtr SetMouseHook(NativeMethods.LowLevelMouseProc proc)
    {
        return NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL, proc,
            NativeMethods.GetModuleHandle(null!), 0);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsEnabled)
        {
            int msg = wParam.ToInt32();

            if (msg == NativeMethods.WM_LBUTTONDOWN)
            {
                _isMouseDown = true;
                NativeMethods.GetCursorPos(out _mouseDownPoint);
                _debounceTimer.Stop();
                SelectionCleared?.Invoke();
            }
            else if (msg == NativeMethods.WM_LBUTTONUP && _isMouseDown)
            {
                _isMouseDown = false;
                long now = Stopwatch.GetTimestamp();

                if (NativeMethods.GetCursorPos(out var upPoint))
                {
                    int dx = Math.Abs(upPoint.X - _mouseDownPoint.X);
                    int dy = Math.Abs(upPoint.Y - _mouseDownPoint.Y);

                    // Case 1: Drag selection (mouse moved past drag threshold)
                    bool isDrag = dx > MinDragDistance || dy > MinDragDistance;

                    // Case 2: Double-click or triple-click selection
                    double elapsedMs = (now - _lastUpTimestamp) * 1000.0 / Stopwatch.Frequency;
                    int doubleClickDist = Math.Abs(upPoint.X - _lastUpPoint.X) + Math.Abs(upPoint.Y - _lastUpPoint.Y);
                    bool isDoubleClick = elapsedMs < 500 && doubleClickDist < 8;

                    _lastUpTimestamp = now;
                    _lastUpPoint = upPoint;

                    if (isDrag || isDoubleClick)
                    {
                        _debounceTimer.Stop();
                        _debounceTimer.Start();
                    }
                }
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    private void OnDebounceTimerTick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();

        if (!IsEnabled) return;

        if (NativeMethods.GetCursorPos(out var point))
        {
            Log.Debug($"[TextSelectionMonitor] Possible selection at ({point.X}, {point.Y})");
            PossibleSelection?.Invoke(point.X, point.Y);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _debounceTimer.Stop();
        Stop();
    }
}
