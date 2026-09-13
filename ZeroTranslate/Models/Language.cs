namespace ZeroTranslate.Models;

/// <summary>
/// Represents a language supported by translation engines.
/// </summary>
public record Language(string Code, string Name, string NativeName)
{
    /// <summary>Auto-detect language (source only).</summary>
    public static readonly Language Auto = new("auto", "Auto Detect", "Tự động");

    public override string ToString() => $"{Name} ({NativeName})";
}
