using ZeroTranslate.Models;

namespace ZeroTranslate.Core.Interfaces;

public interface ISettingsService
{
    AppSettings Settings { get; }
    void Load();
    void Save();
}
