using ZeroTranslate.Models;

namespace ZeroTranslate.Services;

/// <summary>
/// Registry for translation engines. Makes it easy to add new engines later.
/// Usage:
///   registry.Register(new GoogleTranslateEngine());
///   registry.Register(new BingTranslateEngine());  // future
///   var engine = registry.GetEngine("Google Translate");
/// </summary>
public class TranslationEngineRegistry
{
    private readonly Dictionary<string, ITranslationEngine> _engines = new();
    private string _activeEngineName = "";

    public IReadOnlyCollection<string> AvailableEngines => _engines.Keys;

    public ITranslationEngine ActiveEngine =>
        _engines.TryGetValue(_activeEngineName, out var engine)
            ? engine
            : _engines.Values.First();

    public string ActiveEngineName
    {
        get => _activeEngineName;
        set
        {
            if (_engines.ContainsKey(value))
                _activeEngineName = value;
        }
    }

    /// <summary>
    /// Register a new translation engine.
    /// </summary>
    public void Register(ITranslationEngine engine)
    {
        _engines[engine.Name] = engine;
        if (_engines.Count == 1)
            _activeEngineName = engine.Name;
    }

    /// <summary>
    /// Get engine by name.
    /// </summary>
    public ITranslationEngine? GetEngine(string name) =>
        _engines.TryGetValue(name, out var engine) ? engine : null;

    /// <summary>
    /// Get all registered engines.
    /// </summary>
    public IEnumerable<ITranslationEngine> GetAllEngines() => _engines.Values;
}
