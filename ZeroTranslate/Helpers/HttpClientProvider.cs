using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace ZeroTranslate.Helpers;

/// <summary>
/// Provides a single shared <see cref="HttpClient"/> for all translation engines.
/// Reusing one client reuses TCP connections and the connection pool (faster from
/// the second request) and avoids socket exhaustion. A sane timeout prevents the
/// UI from waiting on a hung request for the default 100 seconds.
/// </summary>
public static class HttpClientProvider
{
    public static HttpClient Shared { get; } = CreateClient();

    private static HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(5)
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        return client;
    }
}
