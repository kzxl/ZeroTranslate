using ZeroTranslate.Helpers;
using ZeroTranslate.Models;
using ZeroTranslate.Services;
using Xunit;

namespace ZeroTranslate.Tests;

public class TranslationServiceTests
{
    /// <summary>A controllable fake engine for testing the service in isolation.</summary>
    private sealed class FakeEngine : ITranslationEngine
    {
        public string Name { get; }
        public bool Succeed { get; set; } = true;
        public int CallCount { get; private set; }

        public FakeEngine(string name) => Name = name;

        public IReadOnlyList<Language> SupportedLanguages => LanguageDatabase.Languages;

        public Task<TranslationResult> TranslateAsync(string text, string sourceLang, string targetLang, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(new TranslationResult
            {
                SourceText = text,
                TranslatedText = Succeed ? $"{Name}:{text}" : "",
                SourceLanguageCode = sourceLang,
                TargetLanguageCode = targetLang,
                DetectedLanguageCode = sourceLang,
                EngineName = Name,
                IsSuccess = Succeed,
                ErrorMessage = Succeed ? null : "fail"
            });
        }

        public Task<string> DetectLanguageAsync(string text, CancellationToken ct = default)
            => Task.FromResult("en");
    }

    private static (TranslationService svc, FakeEngine primary, FakeEngine secondary) BuildService()
    {
        var primary = new FakeEngine("Primary");
        var secondary = new FakeEngine("Secondary");
        var registry = new TranslationEngineRegistry();
        registry.Register(primary);
        registry.Register(secondary);
        registry.ActiveEngineName = "Primary";
        return (new TranslationService(registry), primary, secondary);
    }

    [Fact]
    public async Task Translate_ReturnsActiveEngineResult()
    {
        var (svc, _, _) = BuildService();
        var result = await svc.TranslateAsync("hello", "en", "vi");

        Assert.True(result.IsSuccess);
        Assert.Equal("Primary:hello", result.TranslatedText);
    }

    [Fact]
    public async Task Translate_CachesResult_DoesNotCallEngineTwice()
    {
        var (svc, primary, _) = BuildService();

        await svc.TranslateAsync("hello", "en", "vi");
        await svc.TranslateAsync("hello", "en", "vi");

        Assert.Equal(1, primary.CallCount); // second call served from cache
    }

    [Fact]
    public async Task Translate_DifferentText_NotCached()
    {
        var (svc, primary, _) = BuildService();

        await svc.TranslateAsync("hello", "en", "vi");
        await svc.TranslateAsync("world", "en", "vi");

        Assert.Equal(2, primary.CallCount);
    }

    [Fact]
    public async Task Fallback_UsesSecondaryWhenPrimaryFails()
    {
        var (svc, primary, secondary) = BuildService();
        primary.Succeed = false;
        svc.EnableFallback = true;

        var result = await svc.TranslateAsync("hello", "en", "vi");

        Assert.True(result.IsSuccess);
        Assert.Equal("Secondary:hello", result.TranslatedText);
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(1, secondary.CallCount);
    }

    [Fact]
    public async Task Fallback_Disabled_ReturnsFailure()
    {
        var (svc, primary, secondary) = BuildService();
        primary.Succeed = false;
        svc.EnableFallback = false;

        var result = await svc.TranslateAsync("hello", "en", "vi");

        Assert.False(result.IsSuccess);
        Assert.Equal(0, secondary.CallCount);
    }

    [Fact]
    public async Task ClearCache_ForcesReTranslation()
    {
        var (svc, primary, _) = BuildService();

        await svc.TranslateAsync("hello", "en", "vi");
        svc.ClearCache();
        await svc.TranslateAsync("hello", "en", "vi");

        Assert.Equal(2, primary.CallCount);
    }
}
