using System.Windows.Input;
using ZeroTranslate.Core.Interfaces;
using ZeroTranslate.Helpers;
using ZeroTranslate.Models;
using ZeroTranslate.Services;

namespace ZeroTranslate.ViewModels;

/// <summary>
/// ViewModel for the main translation window.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly TranslationService _translationService;
    private readonly ITtsService? _ttsService;
    private readonly HistoryService? _historyService;

    // --- Bindable Properties ---

    private string _sourceText = "";
    public string SourceText
    {
        get => _sourceText;
        set
        {
            if (SetProperty(ref _sourceText, value))
                OnPropertyChanged(nameof(CharacterCount));
        }
    }

    private string _translatedText = "";
    public string TranslatedText
    {
        get => _translatedText;
        set => SetProperty(ref _translatedText, value);
    }

    private Language _sourceLanguage;
    public Language SourceLanguage
    {
        get => _sourceLanguage;
        set => SetProperty(ref _sourceLanguage, value);
    }

    private Language _targetLanguage;
    public Language TargetLanguage
    {
        get => _targetLanguage;
        set => SetProperty(ref _targetLanguage, value);
    }

    private bool _isTranslating;
    public bool IsTranslating
    {
        get => _isTranslating;
        set => SetProperty(ref _isTranslating, value);
    }

    private string _statusText = "Ready";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private string _lastDetectedLangCode = "";
    private CancellationTokenSource? _cts;

    public int CharacterCount => SourceText?.Length ?? 0;

    public IReadOnlyList<Language> SourceLanguages => LanguageDatabase.SourceLanguages;
    public IReadOnlyList<Language> TargetLanguages => LanguageDatabase.TargetLanguages;

    // --- Commands ---

    public ICommand TranslateCommand { get; }
    public ICommand SwapLanguagesCommand { get; }
    public ICommand CopyResultCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand SpeakSourceCommand { get; }
    public ICommand SpeakResultCommand { get; }
    public ICommand UseHistoryItemCommand { get; }
    public ICommand ClearHistoryCommand { get; }

    public bool CanSpeak => _ttsService?.IsAvailable ?? false;

    /// <summary>History entries for the side panel (null when no history service).</summary>
    public System.Collections.ObjectModel.ObservableCollection<HistoryItem>? History => _historyService?.Items;

    public MainViewModel(TranslationService translationService, ITtsService? ttsService = null, HistoryService? historyService = null)
    {
        _translationService = translationService;
        _ttsService = ttsService;
        _historyService = historyService;

        _sourceLanguage = LanguageDatabase.FindByCode("auto") ?? Language.Auto;
        _targetLanguage = LanguageDatabase.FindByCode("vi")
                          ?? LanguageDatabase.TargetLanguages[0];

        TranslateCommand = new AsyncRelayCommand(TranslateAsync, () => !IsTranslating && !string.IsNullOrWhiteSpace(SourceText));
        SwapLanguagesCommand = new RelayCommand(SwapLanguages, () => SourceLanguage.Code != "auto");
        CopyResultCommand = new RelayCommand(CopyResult, () => !string.IsNullOrEmpty(TranslatedText));
        ClearCommand = new RelayCommand(Clear);
        SpeakSourceCommand = new RelayCommand(
            () => _ = _ttsService?.SpeakAsync(SourceText, SourceLanguage?.Code),
            () => CanSpeak && !string.IsNullOrWhiteSpace(SourceText));
        SpeakResultCommand = new RelayCommand(
            () => _ = _ttsService?.SpeakAsync(TranslatedText, TargetLanguage?.Code),
            () => CanSpeak && !string.IsNullOrEmpty(TranslatedText));
        UseHistoryItemCommand = new RelayCommand(UseHistoryItem);
        ClearHistoryCommand = new RelayCommand(() => _historyService?.Clear());
    }

    private void UseHistoryItem(object? parameter)
    {
        if (parameter is not HistoryItem item) return;

        SourceText = item.SourceText;
        TranslatedText = item.TranslatedText;

        var src = LanguageDatabase.FindByCode(item.SourceLanguageCode);
        if (src != null) SourceLanguage = src;
        var tgt = LanguageDatabase.FindByCode(item.TargetLanguageCode);
        if (tgt != null) TargetLanguage = tgt;

        StatusText = $"Từ lịch sử: {item.Timestamp:HH:mm dd/MM}";
    }

    public async Task TranslateAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceText)) return;

        // Cancel any in-flight translation so a rapid re-trigger does not race.
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsTranslating = true;
        StatusText = "Đang dịch...";

        bool autoSwitched = false;
    retry:
        try
        {
            var result = await _translationService.TranslateAsync(
                SourceText, SourceLanguage.Code, TargetLanguage.Code, token);

            if (token.IsCancellationRequested)
                return;

            if (result.IsSuccess)
            {
                if (SourceLanguage.Code == "auto" && !autoSwitched && !string.IsNullOrEmpty(result.DetectedLanguageCode))
                {
                    string detected = result.DetectedLanguageCode.Split('-')[0].ToLower();
                    string currentTarget = TargetLanguage.Code.Split('-')[0].ToLower();

                    if (detected == currentTarget)
                    {
                        string newTargetLang = (detected == "vi") ? "en" : "vi";
                        TargetLanguage = TargetLanguages.FirstOrDefault(l => l.Code == newTargetLang) ?? TargetLanguage;
                        autoSwitched = true;
                        
                        goto retry;
                    }
                }

                TranslatedText = result.TranslatedText;

                // Record into history (deduped, capped, persisted by the service).
                _historyService?.Add(result);

                // Update detected language display
                if (SourceLanguage.Code == "auto" && !string.IsNullOrEmpty(result.DetectedLanguageCode))
                {
                    var detected = LanguageDatabase.FindByCode(result.DetectedLanguageCode);
                    StatusText = detected != null
                        ? $"Phát hiện: {detected.Name} → {TargetLanguage.Name}"
                        : $"Dịch thành công";
                }
                else
                {
                    StatusText = $"{SourceLanguage.Name} → {TargetLanguage.Name}";
                }
            }
            else
            {
                TranslatedText = $"⚠ Lỗi: {result.ErrorMessage}";
                StatusText = "Dịch thất bại";
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer request — ignore.
            return;
        }
        catch (Exception ex)
        {
            TranslatedText = $"⚠ Lỗi: {ex.Message}";
            StatusText = "Dịch thất bại";
        }
        finally
        {
            if (!token.IsCancellationRequested)
                IsTranslating = false;
        }
    }

    private void SwapLanguages()
    {
        if (SourceLanguage.Code == "auto")
        {
            if (string.IsNullOrEmpty(_lastDetectedLangCode) || _lastDetectedLangCode == "auto")
                return;

            var newTarget = TargetLanguages.FirstOrDefault(l => l.Code == _lastDetectedLangCode);
            if (newTarget == null) return;
            
            var oldTarget = TargetLanguage;
            TargetLanguage = newTarget;
            SourceLanguage = SourceLanguages.FirstOrDefault(l => l.Code == oldTarget.Code) ?? SourceLanguages[0];
        }
        else
        {
            var oldTarget = TargetLanguage;
            TargetLanguage = TargetLanguages.FirstOrDefault(l => l.Code == SourceLanguage.Code) ?? TargetLanguages[0];
            SourceLanguage = SourceLanguages.FirstOrDefault(l => l.Code == oldTarget.Code) ?? SourceLanguages[0];
        }

        (SourceText, TranslatedText) = (TranslatedText, SourceText);
        
        // Auto trigger translate after swapping, similar to QTranslate
        if (!string.IsNullOrWhiteSpace(SourceText))
            _ = TranslateAsync();
    }

    private void CopyResult()
    {
        if (!string.IsNullOrEmpty(TranslatedText))
        {
            try { System.Windows.Clipboard.SetText(TranslatedText); } catch { /* ignore */ }
            StatusText = "✓ Đã sao chép bản dịch";
        }
    }

    private void Clear()
    {
        SourceText = "";
        TranslatedText = "";
        StatusText = "Ready";
    }
}
