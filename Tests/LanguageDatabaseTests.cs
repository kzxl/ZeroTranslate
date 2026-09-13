using ZeroTranslate.Helpers;
using Xunit;

namespace ZeroTranslate.Tests;

public class LanguageDatabaseTests
{
    [Fact]
    public void TargetLanguages_ExcludesAuto()
    {
        Assert.DoesNotContain(LanguageDatabase.TargetLanguages, l => l.Code == "auto");
    }

    [Fact]
    public void SourceLanguages_IncludesAuto()
    {
        Assert.Contains(LanguageDatabase.SourceLanguages, l => l.Code == "auto");
    }

    [Fact]
    public void TargetLanguages_IsCachedSameInstance()
    {
        // Cached property should return the same reference each call.
        Assert.Same(LanguageDatabase.TargetLanguages, LanguageDatabase.TargetLanguages);
    }

    [Theory]
    [InlineData("vi", "Vietnamese")]
    [InlineData("en", "English")]
    [InlineData("VI", "Vietnamese")] // case-insensitive
    public void FindByCode_ReturnsExpectedLanguage(string code, string expectedName)
    {
        var lang = LanguageDatabase.FindByCode(code);
        Assert.NotNull(lang);
        Assert.Equal(expectedName, lang!.Name);
    }

    [Fact]
    public void FindByCode_UnknownReturnsNull()
    {
        Assert.Null(LanguageDatabase.FindByCode("zzz"));
    }

    [Fact]
    public void FindByCode_EmptyReturnsNull()
    {
        Assert.Null(LanguageDatabase.FindByCode(""));
    }
}
