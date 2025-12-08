using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SubtitleReader.Models;

namespace SubtitleReader.Services;

/// <summary>
/// Сервис мониторинга областей экрана с OCR распознаванием
/// </summary>
public sealed class RegionMonitorService : IDisposable
{
    private readonly OcrService _ocrService;
    private readonly TextToSpeechService _ttsService;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _monitoringTasks = new();
    private readonly ConcurrentDictionary<string, string> _lastTexts = new();
    private bool _isDisposed;

    private const double SimilarityThreshold = 0.9;

    public event EventHandler<TextChangedEventArgs>? TextChanged;
    public event EventHandler<RegionErrorEventArgs>? Error;

    public RegionMonitorService(OcrService ocrService, TextToSpeechService ttsService)
    {
        _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
        _ttsService = ttsService ?? throw new ArgumentNullException(nameof(ttsService));
    }

    /// <summary>
    /// Начинает мониторинг области - будет постоянно следить за изменениями
    /// </summary>
    public void StartMonitoring(TextRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);

        // Если уже мониторим - останавливаем
        if (_monitoringTasks.ContainsKey(region.Id))
        {
            StopMonitoring(region);
        }

        _lastTexts[region.Id] = string.Empty;

        var cts = new CancellationTokenSource();
        _monitoringTasks[region.Id] = cts;
        region.IsMonitoring = true;

        _ = Task.Run(() => MonitorRegionContinuouslyAsync(region, cts.Token));
        
        Debug.WriteLine($"[Monitor] Запущен: {region.Name}");
    }

    /// <summary>
    /// Останавливает мониторинг области
    /// </summary>
    public void StopMonitoring(TextRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);

        if (_monitoringTasks.TryRemove(region.Id, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
        _lastTexts.TryRemove(region.Id, out _);
        region.IsMonitoring = false;
        
        Debug.WriteLine($"[Monitor] Остановлен: {region.Name}");
    }

    /// <summary>
    /// Останавливает мониторинг всех областей
    /// </summary>
    public void StopAllMonitoring()
    {
        foreach (var cts in _monitoringTasks.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _monitoringTasks.Clear();
        _lastTexts.Clear();
        
        Debug.WriteLine("[Monitor] Все остановлены");
    }

    /// <summary>
    /// Проверяет, мониторится ли область
    /// </summary>
    public bool IsMonitoring(TextRegion region) => 
        region != null && _monitoringTasks.ContainsKey(region.Id);

    /// <summary>
    /// Основной цикл мониторинга - работает постоянно пока не остановят
    /// </summary>
    private async Task MonitorRegionContinuouslyAsync(TextRegion region, CancellationToken token)
    {
        Debug.WriteLine($"[Monitor] Цикл запущен: {region.Name}");

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!region.IsActive)
                {
                    await Task.Delay(region.MonitorIntervalMs, token);
                    continue;
                }

                var recognizedText = await _ocrService.RecognizeRegionAsync(region.Bounds);
                var normalizedText = NormalizeText(recognizedText);
                var lastText = _lastTexts.GetValueOrDefault(region.Id, string.Empty);

                var textChanged = !string.IsNullOrWhiteSpace(normalizedText) && 
                                  !IsSimilarText(normalizedText, lastText);

                if (textChanged)
                {
                    _lastTexts[region.Id] = normalizedText;
                    
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        region.LastRecognizedText = recognizedText;
                        region.Text = recognizedText;
                    });

                    TextChanged?.Invoke(this, new TextChangedEventArgs(region, recognizedText));

                    if (region.AutoRead)
                    {
                        _ttsService.SpeakAsyncNonBlocking(recognizedText, region.ReadingSpeed);
                    }
                }

                await Task.Delay(region.MonitorIntervalMs, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Monitor] Ошибка: {ex.Message}");
                Error?.Invoke(this, new RegionErrorEventArgs(region, ex));
                
                try { await Task.Delay(1000, token); }
                catch (OperationCanceledException) { break; }
            }
        }

        region.IsMonitoring = false;
    }

    /// <summary>
    /// Нормализует текст для сравнения
    /// </summary>
    private static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return string.Join(" ", text.Split(default(char[]), StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Проверяет похожи ли тексты (учитывает OCR ошибки)
    /// </summary>
    private static bool IsSimilarText(string text1, string text2)
    {
        if (string.IsNullOrEmpty(text1) && string.IsNullOrEmpty(text2))
            return true;
        
        if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2))
            return false;

        if (text1 == text2)
            return true;

        return CalculateSimilarity(text1, text2) > SimilarityThreshold;
    }

    /// <summary>
    /// Вычисляет схожесть двух строк (0.0 - 1.0)
    /// </summary>
    private static double CalculateSimilarity(string s1, string s2)
    {
        if (s1 == s2) return 1.0;
        
        var maxLen = Math.Max(s1.Length, s2.Length);
        if (maxLen == 0) return 1.0;
        
        return 1.0 - (double)LevenshteinDistance(s1, s2) / maxLen;
    }

    /// <summary>
    /// Оптимизированный алгоритм Левенштейна с одним массивом
    /// </summary>
    private static int LevenshteinDistance(string s1, string s2)
    {
        var len1 = s1.Length;
        var len2 = s2.Length;
        
        var prev = new int[len2 + 1];
        var curr = new int[len2 + 1];

        for (var j = 0; j <= len2; j++)
            prev[j] = j;

        for (var i = 1; i <= len1; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= len2; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(prev[j] + 1, curr[j - 1] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }

        return prev[len2];
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            StopAllMonitoring();
            _isDisposed = true;
        }
    }
}

public class TextChangedEventArgs : EventArgs
{
    public TextRegion Region { get; }
    public string NewText { get; }

    public TextChangedEventArgs(TextRegion region, string newText)
    {
        Region = region;
        NewText = newText;
    }
}

public class RegionErrorEventArgs : EventArgs
{
    public TextRegion Region { get; }
    public Exception Exception { get; }

    public RegionErrorEventArgs(TextRegion region, Exception exception)
    {
        Region = region;
        Exception = exception;
    }
}
