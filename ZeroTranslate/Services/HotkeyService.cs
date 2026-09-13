using System.Windows.Forms;
using System.Windows.Interop;
using ZeroTranslate.Core.Interfaces;
using ZeroTranslate.Native;

namespace ZeroTranslate.Services;

/// <summary>
/// Manages global hotkey registration via Win32 RegisterHotKey/UnregisterHotKey.
/// Uses HwndSource directly as message sink.
/// </summary>
public class HotkeyService : IHotkeyService
{
    public event Action<int>? HotkeyPressed;

    private HwndSource? _hwndSource;
    private readonly HashSet<int> _registeredIds = new();

    public bool IsRegistered(int hotkeyId = 9000) => _registeredIds.Contains(hotkeyId);

    public HotkeyService()
    {
        var parameters = new HwndSourceParameters("XTranslateHotkeyMsgSink")
        {
            Width = 0,
            Height = 0,
            PositionX = -100,
            PositionY = -100,
            WindowStyle = 0
        };

        _hwndSource = new HwndSource(parameters);
        _hwndSource.AddHook(WndProc);
    }

    public bool RegisterHotkey(Keys hotkey, int hotkeyId = 9000)
    {
        UnregisterHotkey(hotkeyId);

        if (_hwndSource == null) return false;

        var modifiers = GetModifiers(hotkey);
        var vk = (uint)(hotkey & Keys.KeyCode);

        bool success = NativeMethods.RegisterHotKey(_hwndSource.Handle, hotkeyId, modifiers, vk);
        if (success) _registeredIds.Add(hotkeyId);
        return success;
    }

    public void UnregisterHotkey(int hotkeyId = 9000)
    {
        if (_registeredIds.Contains(hotkeyId) && _hwndSource != null)
        {
            NativeMethods.UnregisterHotKey(_hwndSource.Handle, hotkeyId);
            _registeredIds.Remove(hotkeyId);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int hotkeyId = wParam.ToInt32();
            HotkeyPressed?.Invoke(hotkeyId);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static uint GetModifiers(Keys hotkey)
    {
        uint modifiers = 0;
        if (hotkey.HasFlag(Keys.Alt)) modifiers |= 0x0001;
        if (hotkey.HasFlag(Keys.Control)) modifiers |= 0x0002;
        if (hotkey.HasFlag(Keys.Shift)) modifiers |= 0x0004;
        return modifiers;
    }

    public void Dispose()
    {
        var ids = _registeredIds.ToList();
        foreach (var id in ids)
            UnregisterHotkey(id);

        _hwndSource?.RemoveHook(WndProc);
        _hwndSource?.Dispose();
        _hwndSource = null;
    }
}
