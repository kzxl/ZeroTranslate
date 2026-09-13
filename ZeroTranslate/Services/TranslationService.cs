using ZeroTranslate.Models;

namespace ZeroTranslate.Services;

/// <summary>
/// Orchestrator for translation. Uses the engine registry for multi-engine
/// support, provides a thread-safe LRU in-memory cache, and can fall back to
/// other engines when the active one fails.
/// </summary>
public class TranslationService
{
    private readonly TranslationEngineRegistry _registry;

    // LRU cache: a dictionary for O(1) lookup plus a linked list to track
    // access order. Guarded by _cacheLock for thread safety.
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _cacheMap = new();
    private readonly LinkedList<CacheEntry> _lruList = new();
    private readonly object _cacheLock = new();
    private const int MaxCacheSize = 200;

    /// <summary>When true, failed translations retry with the remaining engines.</summary>
    public bool EnableFallback { get; set; } = true;

    public TranslationEngineRegistry Registry => _registry;

    public TranslationService(TranslationEngineRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Translate using the active engine from the registry.
    /// </summary>
    public Task<TranslationResult> TranslateAsync(
        string text, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        return TranslateWithEngineAsync(_registry.ActiveEngine, text, sourceLang, targetLang, ct);
    }

    /// <summary>
    /// Translate using a specific engine by name.
    /// </summary>
    public Task<TranslationResult> TranslateWithEngineAsync(
        string engineName, string text, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        var engine = _registry.GetEngine(engineName) ?? _registry.ActiveEngine;
        return TranslateWithEngineAsync(engine, text, sourceLang, targetLang, ct);
    }

    private async Task<TranslationResult> TranslateWithEngineAsync(
        ITranslationEngine engine, string text, string sourceLang, string targetLang, CancellationToken ct)
    {
        var cacheKey = BuildKey(engine.Name, sourceLang, targetLang, text);

        if (TryGetCached(cacheKey, out var cached))
            return cached;

        var result = await engine.TranslateAsync(text, sourceLang, targetLang, ct);

        // Fallback: if the active engine failed (and the request wasn't cancelled),
        // try the other registered engines in turn until one succeeds.
        if (!result.IsSuccess && EnableFallback && !ct.IsCancellationRequested)
        {
            foreach (var alt in _registry.GetAllEngines())
            {
                if (alt.Name == engine.Name) continue;
                if (ct.IsCancellationRequested) break;

                Log.Debug($"Translation fallback: trying '{alt.Name}' after '{engine.Name}' failed");

                var altKey = BuildKey(alt.Name, sourceLang, targetLang, text);
                if (TryGetCached(altKey, out var altCached))
                    return altCached;

                var altResult = await alt.TranslateAsync(text, sourceLang, targetLang, ct);
                if (altResult.IsSuccess)
                {
                    Store(altKey, altResult);
                    return altResult;
                }
            }
        }

        if (result.IsSuccess)
            Store(cacheKey, result);

        return result;
    }

    private static string BuildKey(string engineName, string sourceLang, string targetLang, string text) =>
        $"{engineName}|{sourceLang}|{targetLang}|{text}";

    private bool TryGetCached(string key, out TranslationResult result)
    {
        lock (_cacheLock)
        {
            if (_cacheMap.TryGetValue(key, out var node))
            {
                // Move to front (most-recently-used).
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                result = node.Value.Result;
                return true;
            }
        }

        result = null!;
        return false;
    }

    private void Store(string key, TranslationResult result)
    {
        lock (_cacheLock)
        {
            if (_cacheMap.TryGetValue(key, out var existing))
            {
                existing.Value = new CacheEntry(key, result);
                _lruList.Remove(existing);
                _lruList.AddFirst(existing);
                return;
            }

            var node = new LinkedListNode<CacheEntry>(new CacheEntry(key, result));
            _lruList.AddFirst(node);
            _cacheMap[key] = node;

            // Evict the genuinely least-recently-used entry when over capacity.
            if (_cacheMap.Count > MaxCacheSize)
            {
                var lru = _lruList.Last;
                if (lru != null)
                {
                    _lruList.RemoveLast();
                    _cacheMap.Remove(lru.Value.Key);
                }
            }
        }
    }

    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _cacheMap.Clear();
            _lruList.Clear();
        }
    }

    private readonly struct CacheEntry
    {
        public string Key { get; }
        public TranslationResult Result { get; }

        public CacheEntry(string key, TranslationResult result)
        {
            Key = key;
            Result = result;
        }
    }
}
