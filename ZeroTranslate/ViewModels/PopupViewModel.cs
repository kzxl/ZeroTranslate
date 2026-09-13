using System.Windows.Input;
using ZeroTranslate.Core.Interfaces;
using ZeroTranslate.Helpers;
using ZeroTranslate.Models;
using ZeroTranslate.Services;

namespace ZeroTranslate.ViewModels;

/// <summary>
/// Lightweight ViewModel for the translation popup overlay.
/// </summary>
public class PopupViewModel : ViewModelBase
{
    private readonly TranslationService _translationService;
    private readonly ITtsService? _ttsService;
    private readonly HistoryService? _historyService;

    private string _sourceText = "";
    public string SourceText
    {
        get => _sourceText;
        set => SetProperty(ref _sourceText, value);
    }

    private string _translatedText = "";
    public string TranslatedText
    {
        get => _translatedText;
        set => SetProperty(ref _translatedText, value);
    }

    private string _lastDetectedLangCode = "";

    private string _detectedLanguage = "";
    public string DetectedLanguage
    {
        get => _detectedLanguage;
        set => SetProperty(ref _detectedLanguage, value);
    }

    private bool _isTranslating;
    public bool IsTranslating
    {
        get => _isTranslating;
        set => SetProperty(ref _isTranslating, value);
    }

    private bool _hasError;
    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    public IReadOnlyList<Language> Languages => LanguageDatabase.Languages;

    private Language _sourceLanguage;
    public Language SourceLanguage
    {
        get => _sourceLanguage;
        set
        {
            if (SetProperty(ref _sourceLanguage, value))
            {
                if (!_isInitializing && !string.IsNullOrWhiteSpace(SourceText))
                    _ = TranslateAsync(SourceText, TargetLanguage?.Code ?? "vi", SourceLanguage?.Code ?? "auto");
            }
        }
    }

    private Language _targetLanguage;
    public Language TargetLanguage
    {
        get => _targetLanguage;
        set
        {
            if (SetProperty(ref _targetLanguage, value))
            {
                if (!_isInitializing && !string.IsNullOrWhiteSpace(SourceText))
                    _ = TranslateAsync(SourceText, TargetLanguage?.Code ?? "vi", SourceLanguage?.Code ?? "auto");
            }
        }
    }

    private bool _isInitializing = true;
    private CancellationTokenSource? _cts;
    public ICommand CopyCommand { get; }
    public ICommand SwapLanguagesCommand { get; }
    public ICommand SpeakCommand { get; }

    public bool CanSpeak => _ttsService?.IsAvailable ?? false;

    public PopupViewModel(TranslationService translationService, ITtsService? ttsService = null, HistoryService? historyService = null)
    {
        _translationService = translationService;
        _ttsService = ttsService;
        _historyService = historyService;
        
        _sourceLanguage = Languages.FirstOrDefault(l => l.Code == "auto") ?? Languages[0];
        _targetLanguage = Languages.FirstOrDefault(l => l.Code == "vi") ?? Languages[0];
        _isInitializing = false;

        SwapLanguagesCommand = new RelayCommand(() =>
        {
            Language? newSource = TargetLanguage;
            Language? newTarget = SourceLanguage;

            if (SourceLanguage?.Code == "auto")
            {
                if (string.IsNullOrEmpty(_lastDetectedLangCode) || _lastDetectedLangCode == "auto")
                    return;

                newTarget = Languages.FirstOrDefault(l => l.Code == _lastDetectedLangCode);
                if (newTarget == null) return;
            }

            _isInitializing = true;
            SourceLanguage = newSource ?? Languages[0];
            _isInitializing = false;
            
            TargetLanguage = newTarget;
        });

        CopyCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrEmpty(TranslatedText))
                System.Windows.Clipboard.SetText(TranslatedText);
        });

        SpeakCommand = new RelayCommand(
            () => _ = _ttsService?.SpeakAsync(TranslatedText, TargetLanguage?.Code),
            () => CanSpeak && !string.IsNullOrEmpty(TranslatedText));
    }

    /// <summary>
    /// Puts the popup into a transient busy state (e.g. while OCR is running)
    /// so the user gets immediate feedback before any text is available.
    /// </summary>
    public void SetBusy(string message)
    {
        SourceText = message;
        TranslatedText = "";
        DetectedLanguage = "";
        HasError = false;
        IsTranslating = true;
    }

    /// <summary>
    /// Translates the given text to the target language.
    /// </summary>
    public async Task TranslateAsync(string text, string targetLang = "vi", string sourceLang = "auto")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            TranslatedText = "";
            return;
        }

        // Cancel any in-flight translation so a rapid language/text change does
        // not race an older request whose result would overwrite the newer one.
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        SourceText = text.Trim();
        IsTranslating = true;
        HasError = false;

        bool autoSwitched = false;
    retry:
        try
        {
            var result = await _translationService.TranslateAsync(SourceText, sourceLang, targetLang, token);

            if (token.IsCancellationRequested)
                return;

            if (result.IsSuccess)
            {
                // Logic thông minh: NẾU dịch ra mà Ngôn ngữ Vừa Detect trùng béng luôn với Ngôn ngữ Đích (ví dụ text TV -> Dịch sang TV).
                // TA sẽ tự động tráo ngôn ngữ đích sang Anh (hoặc Việt nếu là TA) và dịch lại thêm 1 phát nữa.
                if (sourceLang == "auto" && !autoSwitched && !string.IsNullOrEmpty(result.DetectedLanguageCode))
                {
                    string detected = result.DetectedLanguageCode.Split('-')[0].ToLower();
                    string currentTarget = targetLang.Split('-')[0].ToLower();

                    if (detected == currentTarget)
                    {
                        string newTargetLang = (detected == "vi") ? "en" : "vi";
                        targetLang = newTargetLang;
                        autoSwitched = true;

                        // Cập nhật âm thầm lên ComboBox UI mà không làm API bắn lần 3
                        _isInitializing = true;
                        TargetLanguage = Languages.FirstOrDefault(l => l.Code == newTargetLang) ?? TargetLanguage;
                        _isInitializing = false;

                        goto retry;
                    }
                }

                TranslatedText = result.TranslatedText;
                _lastDetectedLangCode = result.DetectedLanguageCode;
                var detectedObj = LanguageDatabase.FindByCode(result.DetectedLanguageCode);
                DetectedLanguage = detectedObj?.Name ?? result.DetectedLanguageCode;

                // Record into shared translation history.
                _historyService?.Add(result);
            }
            else
            {
                TranslatedText = result.ErrorMessage ?? "Translation failed";
                HasError = true;
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer request — ignore.
            return;
        }
        catch (Exception ex)
        {
            TranslatedText = ex.Message;
            HasError = true;
        }
        finally
        {
            if (!token.IsCancellationRequested)
                IsTranslating = false;
        }
    }
}
