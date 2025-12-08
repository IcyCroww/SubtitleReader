using System;
using System.Collections.Generic;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using SubtitleReader.Models;

namespace SubtitleReader.Services;

public class TextToSpeechService : IDisposable
{
    private readonly SpeechSynthesizer _synthesizer;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isDisposed;
    private bool _isSpeaking;
    private int _volume = 100;

    public TextToSpeechService()
    {
        _synthesizer = new SpeechSynthesizer();
        _synthesizer.SpeakCompleted += (s, e) => _isSpeaking = false;
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

    /// <summary>
    /// Получает список доступных голосов
    /// </summary>
    public IReadOnlyCollection<InstalledVoice> GetAvailableVoices()
    {
        return _synthesizer.GetInstalledVoices();
    }

    /// <summary>
    /// Устанавливает голос по имени
    /// </summary>
    public bool SetVoice(string voiceName)
    {
        try
        {
            if (string.IsNullOrEmpty(voiceName))
                return false;

            _synthesizer.SelectVoice(voiceName);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка установки голоса: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Получает текущий голос
    /// </summary>
    public string GetCurrentVoice()
    {
        return _synthesizer.Voice?.Name ?? string.Empty;
    }

    /// <summary>
    /// Читает текст из области асинхронно
    /// </summary>
    public async Task SpeakAsync(TextRegion region)
    {
        if (string.IsNullOrWhiteSpace(region.Text))
            return;

        await SpeakAsync(region.Text, region.ReadingSpeed);
    }

    /// <summary>
    /// Читает текст асинхронно с указанной скоростью
    /// </summary>
    public async Task SpeakAsync(string text, double speedMultiplier = 1.0)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        Stop();

        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        try
        {
            _isSpeaking = true;

            // Преобразуем множитель скорости (0.5-2.0) в диапазон -10 до 10
            int rate = (int)((speedMultiplier - 1.0) * 10);
            rate = Math.Clamp(rate, -10, 10);
            _synthesizer.Rate = rate;

            await Task.Run(() =>
            {
                if (!token.IsCancellationRequested)
                {
                    _synthesizer.Speak(text);
                }
            }, token);
        }
        catch (OperationCanceledException)
        {
            // Отмена - это нормально
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка синтеза речи: {ex.Message}");
        }
        finally
        {
            _isSpeaking = false;
        }
    }

    /// <summary>
    /// Читает текст асинхронно (не блокирует)
    /// </summary>
    public void SpeakAsyncNonBlocking(string text, double speedMultiplier = 1.5)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        try
        {
            _isSpeaking = true;

            // Увеличиваем скорость - делаем более быстрое чтение
            int rate = (int)((speedMultiplier - 1.0) * 10);
            rate = Math.Clamp(rate, -10, 10);
            _synthesizer.Rate = rate;

            // Очищаем очередь и добавляем новый текст
            _synthesizer.SpeakAsyncCancelAll();
            _synthesizer.SpeakAsync(text);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка синтеза речи: {ex.Message}");
            _isSpeaking = false;
        }
    }

    /// <summary>
    /// Останавливает чтение
    /// </summary>
    public void Stop()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            _synthesizer.SpeakAsyncCancelAll();
            _isSpeaking = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка остановки речи: {ex.Message}");
        }
    }

    /// <summary>
    /// Приостанавливает чтение
    /// </summary>
    public void Pause()
    {
        try
        {
            if (_synthesizer.State == SynthesizerState.Speaking)
            {
                _synthesizer.Pause();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка паузы: {ex.Message}");
        }
    }

    /// <summary>
    /// Возобновляет чтение
    /// </summary>
    public void Resume()
    {
        try
        {
            if (_synthesizer.State == SynthesizerState.Paused)
            {
                _synthesizer.Resume();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка возобновления: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            Stop();
            _synthesizer.Dispose();
            _cancellationTokenSource?.Dispose();
            _isDisposed = true;
        }
    }
}
