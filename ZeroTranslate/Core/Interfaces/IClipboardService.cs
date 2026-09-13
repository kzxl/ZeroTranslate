namespace ZeroTranslate.Core.Interfaces;

public interface IClipboardService
{
    Task<string?> GetSelectedTextAsync();
}
