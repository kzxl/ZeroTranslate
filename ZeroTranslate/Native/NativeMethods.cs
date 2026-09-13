using System.Runtime.InteropServices;
using ZeroTranslate.Helpers;

namespace ZeroTranslate.Native;

/// <summary>
/// Minimal Win32 P/Invoke declarations needed for ZeroTranslate.
/// </summary>
internal static partial class NativeMethods
{
    // --- Hotkey Registration ---

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    // --- Foreground Window ---

    [LibraryImport("user32.dll")]
    public static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(IntPtr hWnd);

    // --- Cursor Position ---

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    // --- Clipboard change detection ---

    /// <summary>
    /// Returns the clipboard sequence number. It increments every time the
    /// clipboard content changes, letting us detect a Ctrl+C result without
    /// relying on a fixed delay.
    /// </summary>
    [LibraryImport("user32.dll")]
    public static partial uint GetClipboardSequenceNumber();

    // --- Keyboard Input Simulation (for Ctrl+C) ---

    [LibraryImport("user32.dll")]
    public static partial short GetAsyncKeyState(int vKey);

    [LibraryImport("user32.dll")]
    public static partial uint MapVirtualKey(uint uCode, uint uMapType);

    [LibraryImport("user32.dll")]
    public static partial uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

    public const int INPUT_MOUSE = 0;
    public const int INPUT_KEYBOARD = 1;
    public const int INPUT_HARDWARE = 2;

    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_SCANCODE = 0x0008;

    public const ushort VK_SHIFT = 0x10;
    public const ushort VK_CONTROL = 0x11;
    public const ushort VK_MENU = 0x12; // Alt
    public const ushort VK_LWIN = 0x5B;
    public const ushort VK_RWIN = 0x5C;
    public const ushort VK_C = 0x43;

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint Msg;
        public ushort ParamL;
        public ushort ParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION
    {
        [FieldOffset(0)]
        public MOUSEINPUT Mouse;

        [FieldOffset(0)]
        public KEYBDINPUT Keyboard;

        [FieldOffset(0)]
        public HARDWAREINPUT Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public int Type;
        public INPUTUNION Union;
    }

    // --- Window Messages ---
    public const int WM_HOTKEY = 0x0312;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP = 0x0202;

    // --- GDI Object Cleanup ---
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr hObject);

    // --- Low-level Mouse Hook ---
    public const int WH_MOUSE_LL = 14;

    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    // --- Helper: Simulate Ctrl+C ---

    /// <summary>
    /// Simulate Ctrl+C keystroke to copy selected text to clipboard.
    /// Releases any held modifier keys first to avoid conflicts.
    /// </summary>
    public static void SendCtrlC()
    {
        int inputSize = Marshal.SizeOf<INPUT>();

        // 1. Release modifier keys that might be physically held (from hotkey)
        var releaseInputs = new List<INPUT>();
        if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0)
            releaseInputs.Add(MakeKeyInput(VK_CONTROL, KEYEVENTF_KEYUP));
        if ((GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0)
            releaseInputs.Add(MakeKeyInput(VK_SHIFT, KEYEVENTF_KEYUP));
        if ((GetAsyncKeyState(VK_MENU) & 0x8000) != 0)
            releaseInputs.Add(MakeKeyInput(VK_MENU, KEYEVENTF_KEYUP));

        if (releaseInputs.Count > 0)
        {
            uint relSent = SendInput((uint)releaseInputs.Count, releaseInputs.ToArray(), inputSize);
            Log.Debug($"[SendCtrlC] Released {relSent}/{releaseInputs.Count} modifier(s)");
            Thread.Sleep(25);
        }

        // 2. Send Ctrl+C
        var inputs = new INPUT[]
        {
            MakeKeyInput(VK_CONTROL, 0),
            MakeKeyInput(VK_C, 0),
            MakeKeyInput(VK_C, KEYEVENTF_KEYUP),
            MakeKeyInput(VK_CONTROL, KEYEVENTF_KEYUP),
        };

        uint sent = SendInput((uint)inputs.Length, inputs, inputSize);
        Log.Debug($"[SendCtrlC] SendInput sent {sent}/{inputs.Length} keys (sizeof(INPUT)={inputSize})");
    }

    private static INPUT MakeKeyInput(ushort vk, uint flags) => new()
    {
        Type = INPUT_KEYBOARD,
        Union = new INPUTUNION
        {
            Keyboard = new KEYBDINPUT
            {
                Vk = vk,
                Scan = (ushort)MapVirtualKey(vk, 0),
                Flags = flags,
                Time = 0,
                ExtraInfo = IntPtr.Zero
            }
        }
    };
}
