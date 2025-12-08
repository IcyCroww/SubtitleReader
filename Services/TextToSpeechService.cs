using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using SubtitleReader.Models;

namespace SubtitleReader.Services;

/// <summary>
/// Сервис синтеза речи
/// </summary>
public sealed class TextToSpeechService : IDisposable
{
    private readonly SpeechSynthesizer _synthesizer = new();
    private CancellationTokenSource? _cts;
    private bool _isDisposed;
    private bool _isSpeaking;
    private int _volume = 100;

    public TextToSpeechService()
    {
        _synthesizer.SpeakCompleted += (_, _) => _isSpeaking = false;
        _synthesizer.SetOutputToDefaultAudioDevice();
    }

    public bool IsSpeaking => _isSpeaking;
    
    public int Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 100);
            _synthesizer.Volume = _volume;
        }
    }

    public IReadOnlyCollection<InstalledVoice> GetAvailableVoices() => 
        _synthesizer.GetInstalledVoices();

    public bool SetVoice(string voiceName)
    {
        if (string.IsNullOrEmpty(voiceName))
            return false;

        try
        {
            _synthesizer.SelectVoice(voiceName);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TTS] Ошибка голоса: {ex.Message}");
            return false;
        }
    }

    public string GetCurrentVoice() => _synthesizer.Voice?.Name ?? string.Empty;

    public Task SpeakAsync(TextRegion region) =>
        string.IsNullOrWhiteSpace(region?.Text) ? Task.CompletedTask : SpeakAsync(region.Text, region.ReadingSpeed);

    public async Task SpeakAsync(string text, double speedMultiplier = 1.0)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        Stop();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            _isSpeaking = true;
            _synthesizer.Rate = SpeedToRate(speedMultiplier);

            await Task.Run(() =>
            {
                if (!token.IsCancellationRequested)
                    _synthesizer.Speak(text);
            }, token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TTS] Ошибка: {ex.Message}");
        }
        finally
        {
            _isSpeaking = false;
        }
    }

    public void SpeakAsyncNonBlocking(string text, double speedMultiplier = 1.5)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        try
        {
            _isSpeaking = true;
            _synthesizer.Rate = SpeedToRate(speedMultiplier);
            _synthesizer.SpeakAsyncCancelAll();
            _synthesizer.SpeakAsync(text);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TTS] Ошибка: {ex.Message}");
            _isSpeaking = false;
        }
    }

    private static int SpeedToRate(double speed) => 
        Math.Clamp((int)((speed - 1.0) * 10), -10, 10);

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _synthesizer.SpeakAsyncCancelAll();
            _isSpeaking = false;
        }
        catch { }
    }

    public void Pause()
    {
        if (_synthesizer.State == SynthesizerState.Speaking)
            _synthesizer.Pause();
    }

    public void Resume()
    {
        if (_synthesizer.State == SynthesizerState.Paused)
            _synthesizer.Resume();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        
        Stop();
        _synthesizer.Dispose();
        _cts?.Dispose();
        _isDisposed = true;
    }
}
