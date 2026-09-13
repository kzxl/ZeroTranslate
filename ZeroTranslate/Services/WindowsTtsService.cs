using System.IO;
using System.Windows.Media;
using Windows.Media.SpeechSynthesis;
using ZeroTranslate.Core.Interfaces;

namespace ZeroTranslate.Services;

/// <summary>
/// Text-to-speech using the built-in WinRT <see cref="SpeechSynthesizer"/>.
/// The synthesized audio (WAV) is buffered to a temp file and played through a
/// WPF <see cref="MediaPlayer"/>. No third-party dependency required.
/// </summary>
public class WindowsTtsService : ITtsService, IDisposable
{
    private readonly SpeechSynthesizer _synth = new();
    private readonly MediaPlayer _player = new();
    private string? _currentTempFile;
    private bool _available = true;

    public bool IsAvailable => _available;

    public WindowsTtsService()
    {
        _player.MediaEnded += (_, _) => CleanupTempFile();
    }

    public async Task SpeakAsync(string text, string? languageTag = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        try
        {
            Stop();

            // Pick a voice matching the language when one is installed.
            SelectVoice(languageTag);

            using var stream = await _synth.SynthesizeTextToStreamAsync(text);

            // Persist the WAV stream to a temp file so MediaPlayer can play it.
            var temp = Path.Combine(Path.GetTempPath(),
                $"xtranslate_tts_{Guid.NewGuid():N}.wav");

            using (var netStream = stream.AsStreamForRead())
            using (var fileStream = File.Create(temp))
            {
                await netStream.CopyToAsync(fileStream);
            }

            _currentTempFile = temp;
            _player.Open(new Uri(temp));
            _player.Play();
        }
        catch (Exception ex)
        {
            _available = false;
            Log.Error($"TTS: {ex.Message}");
        }
    }

    private void SelectVoice(string? languageTag)
    {
        if (string.IsNullOrEmpty(languageTag))
            return;

        try
        {
            // Match on the primary subtag (e.g. "en" matches "en-US").
            var primary = languageTag.Split('-')[0];
            var voice = SpeechSynthesizer.AllVoices.FirstOrDefault(v =>
                            v.Language.StartsWith(languageTag, StringComparison.OrdinalIgnoreCase))
                        ?? SpeechSynthesizer.AllVoices.FirstOrDefault(v =>
                            v.Language.StartsWith(primary, StringComparison.OrdinalIgnoreCase));

            if (voice != null)
                _synth.Voice = voice;
        }
        catch
        {
            // Keep the default voice if matching fails.
        }
    }

    public void Stop()
    {
        try
        {
            _player.Stop();
            _player.Close();
        }
        catch { /* ignore */ }
        CleanupTempFile();
    }

    private void CleanupTempFile()
    {
        if (_currentTempFile == null) return;
        try
        {
            if (File.Exists(_currentTempFile))
                File.Delete(_currentTempFile);
        }
        catch { /* best effort */ }
        finally
        {
            _currentTempFile = null;
        }
    }

    public void Dispose()
    {
        Stop();
        _synth.Dispose();
    }
}
