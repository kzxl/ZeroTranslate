using System.Windows.Forms;

namespace ZeroTranslate.Core.Interfaces;

public interface IHotkeyService : IDisposable
{
    event Action<int>? HotkeyPressed;
    bool IsRegistered(int hotkeyId = 9000);
    bool RegisterHotkey(Keys hotkey, int hotkeyId = 9000);
    void UnregisterHotkey(int hotkeyId = 9000);
}
