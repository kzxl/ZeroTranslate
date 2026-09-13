using System.Diagnostics;

namespace ZeroTranslate.Helpers;

/// <summary>
/// Lightweight logging wrapper. In Release builds the Debug() calls are
/// compiled away entirely (no string allocation, no I/O), keeping hot-paths
/// such as the low-level mouse hook and clipboard capture allocation-free.
/// </summary>
public static class Log
{
    /// <summary>
    /// Writes a debug message. Removed by the compiler in Release builds.
    /// </summary>
    [Conditional("DEBUG")]
    public static void Debug(string message) => System.Diagnostics.Debug.WriteLine($"[ZeroTranslate] {message}");

    /// <summary>
    /// Writes a debug message built only when DEBUG is defined.
    /// Use for messages whose construction is itself expensive (Substring, interpolation).
    /// </summary>
    [Conditional("DEBUG")]
    public static void Debug(Func<string> messageFactory) =>
        System.Diagnostics.Debug.WriteLine($"[ZeroTranslate] {messageFactory()}");

    /// <summary>
    /// Always-on error logging (kept in Release). Errors are rare and not on hot-paths.
    /// </summary>
    public static void Error(string message) => System.Diagnostics.Debug.WriteLine($"[ZeroTranslate][ERROR] {message}");
}
