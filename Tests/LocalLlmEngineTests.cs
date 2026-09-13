using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroTranslate.Services;

namespace ZeroTranslate.Tests;

public class LocalLlmEngineTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseJson;

        public MockHttpMessageHandler(string responseJson)
        {
            _responseJson = responseJson;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    [Fact]
    public void LocalLlmTranslateEngine_HasSupportedLanguages()
    {
        var engine = new LocalLlmTranslateEngine();
        Assert.Equal("Local LLM (Ollama / LocalAI)", engine.Name);
        Assert.NotEmpty(engine.SupportedLanguages);
    }

    [Theory]
    [InlineData("Xin chào thế giới", "en")]
    [InlineData("你好世界", "zh")]
    [InlineData("こんにちは世界", "ja")]
    [InlineData("안녕하세요", "ko")]
    public async Task LocalLlmTranslateEngine_DetectsLanguageHeuristically(string input, string expectedLang)
    {
        var engine = new LocalLlmTranslateEngine();
        var detected = await engine.DetectLanguageAsync(input);
        Assert.Equal(expectedLang, detected);
    }

    [Fact]
    public async Task LocalLlmTranslateEngine_ParsesOllamaResponseSuccessfully()
    {
        var mockOllamaJson = """
        {
            "model": "qwen2.5:latest",
            "response": "Hello world from Local LLM",
            "done": true
        }
        """;

        var httpClient = new HttpClient(new MockHttpMessageHandler(mockOllamaJson));
        var engine = new LocalLlmTranslateEngine(httpClient);

        var result = await engine.TranslateAsync("Xin chào thế giới", "vi", "en");

        Assert.True(result.IsSuccess);
        Assert.Equal("Hello world from Local LLM", result.TranslatedText);
        Assert.Equal(engine.Name, result.EngineName);
    }

    [Fact]
    public async Task LocalLlmTranslateEngine_ParsesOpenAiCompatibleResponseSuccessfully()
    {
        var mockOpenAiJson = """
        {
            "choices": [
                {
                    "message": {
                        "role": "assistant",
                        "content": "Bonjour le monde"
                    }
                }
            ]
        }
        """;

        var httpClient = new HttpClient(new MockHttpMessageHandler(mockOpenAiJson));
        var engine = new LocalLlmTranslateEngine(httpClient);

        var result = await engine.TranslateAsync("Hello world", "en", "fr");

        Assert.True(result.IsSuccess);
        Assert.Equal("Bonjour le monde", result.TranslatedText);
    }
}
