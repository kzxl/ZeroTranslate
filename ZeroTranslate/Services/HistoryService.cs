using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using ZeroTranslate.Models;

namespace ZeroTranslate.Services;

/// <summary>
/// Keeps a capped, persisted list of recent translations for the history panel.
/// Stored as JSON in %AppData%/ZeroTranslate/history.json.
/// </summary>
public class HistoryService
{
    private static readonly string HistoryFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ZeroTranslate", "history.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const int MaxItems = 100;

    /// <summary>
    /// Bound directly to the UI. Newest entries are first. All mutations happen
    /// on the UI thread so the collection stays consistent with bindings.
    /// </summary>
    public ObservableCollection<HistoryItem> Items { get; } = new();

    public void Load()
    {
        try
        {
            if (!File.Exists(HistoryFile)) return;

            var json = File.ReadAllText(HistoryFile);
            var items = JsonSerializer.Deserialize<List<HistoryItem>>(json, JsonOptions);
            if (items == null) return;

            Items.Clear();
            foreach (var item in items.Take(MaxItems))
                Items.Add(item);
        }
        catch
        {
            // History is non-critical; ignore load failures.
        }
    }

    public void Add(TranslationResult result)
    {
        if (!result.IsSuccess ||
            string.IsNullOrWhiteSpace(result.SourceText) ||
            string.IsNullOrWhiteSpace(result.TranslatedText))
            return;

        void Mutate()
        {
            // Skip if identical to the most recent entry.
            if (Items.Count > 0 &&
                Items[0].SourceText == result.SourceText &&
                Items[0].TargetLanguageCode == result.TargetLanguageCode)
                return;

            Items.Insert(0, new HistoryItem
            {
                SourceText = result.SourceText,
                TranslatedText = result.TranslatedText,
                SourceLanguageCode = result.SourceLanguageCode,
                TargetLanguageCode = result.TargetLanguageCode,
                EngineName = result.EngineName,
                Timestamp = DateTime.Now
            });

            while (Items.Count > MaxItems)
                Items.RemoveAt(Items.Count - 1);
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
            dispatcher.Invoke(Mutate);
        else
            Mutate();
    }

    public void Clear()
    {
        Items.Clear();
        Save();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(HistoryFile)!);
            var json = JsonSerializer.Serialize(Items.ToList(), JsonOptions);
            File.WriteAllText(HistoryFile, json);
        }
        catch
        {
            // Non-critical.
        }
    }
}
