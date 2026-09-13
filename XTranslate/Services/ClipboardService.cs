using System.Windows;
using XTranslate.Core.Interfaces;
using XTranslate.Helpers;
using XTranslate.Native;

namespace XTranslate.Services;

/// <summary>
/// Captures selected text from any application using clipboard automation.
/// Uses adaptive polling on the clipboard sequence number instead of fixed
/// delays, so capture completes as soon as the target app responds to Ctrl+C
/// (typically 20-80ms) rather than always waiting ~200ms.
/// </summary>
public class ClipboardService : IClipboardService
{
    private const int MaxWaitMs = 500;
    private const int PollIntervalMs = 20;

    /// <summary>
    /// Captures the currently selected text by simulating Ctrl+C.
    /// </summary>
    public async Task<string?> GetSelectedTextAsync()
    {
        var dispatcher = Application.Current.Dispatcher;

        try
        {
            // 1. Snapshot original clipboard and clear it.
            // Capture sequence number AFTER Clear so any new copy bumps it.
            string? original = null;
            uint seqBefore = 0;

            await dispatcher.InvokeAsync(() =>
            {
                try { original = Clipboard.GetText(); }
                catch { original = null; }

                try { Clipboard.Clear(); }
                catch { /* ignore */ }

                seqBefore = NativeMethods.GetClipboardSequenceNumber();
            });

            // 2. Settle and simulate Ctrl+C to foreground window.
            await Task.Delay(20);
            await Task.Run(NativeMethods.SendCtrlC);

            // 3. Poll until the clipboard sequence number advances past the clear baseline.
            string? text = null;
            int waited = 0;

            while (waited < MaxWaitMs)
            {
                await Task.Delay(PollIntervalMs);
                waited += PollIntervalMs;

                var (hasNewText, current) = await dispatcher.InvokeAsync(() =>
                {
                    uint seqCurrent = NativeMethods.GetClipboardSequenceNumber();
                    if (seqCurrent != seqBefore)
                    {
                        try
                        {
                            if (Clipboard.ContainsText())
                            {
                                var t = Clipboard.GetText();
                                return (!string.IsNullOrWhiteSpace(t), t);
                            }
                        }
                        catch
                        {
                            // Clipboard might be locked by source app writing to it; retry on next poll tick
                        }
                    }
                    return (false, (string?)null);
                });

                if (hasNewText && !string.IsNullOrWhiteSpace(current))
                {
                    text = current;
                    break;
                }
            }

            Log.Debug($"[Clipboard] Captured text in {waited}ms: '{Truncate(text, 50)}'");

            // 4. Restore original clipboard content if we have something to restore.
            if (!string.IsNullOrEmpty(original))
            {
                await dispatcher.InvokeAsync(() =>
                {
                    try { Clipboard.SetText(original); }
                    catch { /* ignore */ }
                });
            }

            return text;
        }
        catch (Exception ex)
        {
            Log.Error($"ClipboardService: {ex.Message}");
            return null;
        }
    }

    private static string Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? "" : value.Substring(0, Math.Min(value.Length, max));
}
