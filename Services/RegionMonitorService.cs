using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using SubtitleReader.Models;

namespace SubtitleReader.Services;

public class RegionMonitorService : IDisposable
{
    private readonly OcrService _ocrService;
    private readonly TextToSpeechService _ttsService;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _monitoringTasks;
    private readonly ConcurrentDictionary<string, string> _lastTexts; // Храним последний текст для каждой области
    private bool _isDisposed;

    public event EventHandler<TextChangedEventArgs>? TextChanged;
    public event EventHandler<RegionErrorEventArgs>? Error;

    public RegionMonitorService(OcrService ocrService, TextToSpeechService ttsService)
    {
        _ocrService = ocrService;
        _ttsService = ttsService;
        _monitoringTasks = new ConcurrentDictionary<string, CancellationTokenSource>();
        _lastTexts = new ConcurrentDictionary<string, string>();
    }

    /// <summary>
    /// Начинает мониторинг области - будет постоянно следить за изменениями
    /// </summary>
    public void StartMonitoring(TextRegion region)
    {
        // Если уже мониторим - останавливаем
        if (_monitoringTasks.ContainsKey(region.Id))
        {
            StopMonitoring(region);
        }

        // Сбрасываем последний текст чтобы первое распознавание сработало
        _lastTexts[region.Id] = string.Empty;

        var cts = new CancellationTokenSource();
        _monitoringTasks[region.Id] = cts;
        region.IsMonitoring = true;

        // Запускаем бесконечный цикл мониторинга
        _ = Task.Run(async () => await MonitorRegionContinuouslyAsync(region, cts.Token));
        
        System.Diagnostics.Debug.WriteLine($"[Monitor] Запущен мониторинг области: {region.Name}");
    }

    /// <summary>
    /// Останавливает мониторинг области
    /// </summary>
    public void StopMonitoring(TextRegion region)
    {
        if (_monitoringTasks.TryRemove(region.Id, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
        _lastTexts.TryRemove(region.Id, out _);
        region.IsMonitoring = false;
        
        System.Diagnostics.Debug.WriteLine($"[Monitor] Остановлен мониторинг области: {region.Name}");
    }

    /// <summary>
    /// Останавливает мониторинг всех областей
    /// </summary>
    public void StopAllMonitoring()
    {
        foreach (var kvp in _monitoringTasks)
        {
            kvp.Value.Cancel();
            kvp.Value.Dispose();
        }
        _monitoringTasks.Clear();
        _lastTexts.Clear();
        
        System.Diagnostics.Debug.WriteLine("[Monitor] Остановлен мониторинг всех областей");
    }

    /// <summary>
    /// Проверяет, мониторится ли область
    /// </summary>
    public bool IsMonitoring(TextRegion region)
    {
        return _monitoringTasks.ContainsKey(region.Id);
    }

    /// <summary>
    /// Основной цикл мониторинга - работает ПОСТОЯННО пока не остановят
    /// </summary>
    private async Task MonitorRegionContinuouslyAsync(TextRegion region, CancellationToken token)
    {
        System.Diagnostics.Debug.WriteLine($"[Monitor] Цикл мониторинга запущен для: {region.Name}");

        while (!token.IsCancellationRequested)
        {
            try
            {
                // Пропускаем если область неактивна
                if (!region.IsActive)
                {
                    await Task.Delay(region.MonitorIntervalMs, token);
                    continue;
                }

                // Распознаём текст в области
                var recognizedText = await _ocrService.RecognizeRegionAsync(region.Bounds);
                
                // Нормализуем текст для сравнения
                var normalizedText = NormalizeText(recognizedText);
                var lastText = _lastTexts.GetValueOrDefault(region.Id, string.Empty);

                // Логируем что происходит
                System.Diagnostics.Debug.WriteLine($"[Monitor] {region.Name}: Распознано='{normalizedText}', Последний='{lastText}'");

                // Проверяем изменился ли текст (используем более умное сравнение)
                bool textChanged = !string.IsNullOrWhiteSpace(normalizedText) && 
                                   !IsSimilarText(normalizedText, lastText);

                if (textChanged)
                {
                    System.Diagnostics.Debug.WriteLine($"[Monitor] ✅ НОВЫЙ ТЕКСТ в {region.Name}: '{normalizedText}'");
                    
                    // Сохраняем новый текст
                    _lastTexts[region.Id] = normalizedText;
                    
                    // Обновляем UI через Dispatcher
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        region.LastRecognizedText = recognizedText;
                        region.Text = recognizedText;
                    });

                    // Уведомляем об изменении
                    TextChanged?.Invoke(this, new TextChangedEventArgs(region, recognizedText));

                    // Читаем если включено авто-чтение
                    if (region.AutoRead)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Monitor] 🔊 Читаю текст со скоростью {region.ReadingSpeed}x");
                        _ttsService.SpeakAsyncNonBlocking(recognizedText, region.ReadingSpeed);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(normalizedText))
                {
                    System.Diagnostics.Debug.WriteLine($"[Monitor] ⏸ Текст не изменился в {region.Name}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Monitor] ❌ Пустой текст в {region.Name}");
                }

                // Ждём перед следующей проверкой
                await Task.Delay(region.MonitorIntervalMs, token);
            }
            catch (OperationCanceledException)
            {
                // Нормальная остановка
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Monitor] Ошибка: {ex.Message}");
                Error?.Invoke(this, new RegionErrorEventArgs(region, ex));
                
                // Ждём перед повторной попыткой
                try
                {
                    await Task.Delay(1000, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        region.IsMonitoring = false;
        System.Diagnostics.Debug.WriteLine($"[Monitor] Цикл мониторинга завершён для: {region.Name}");
    }

    /// <summary>
    /// Нормализует текст для сравнения (убирает лишние пробелы, переносы)
    /// </summary>
    private static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Убираем лишние пробелы и переносы строк
        return string.Join(" ", text.Split(new[] { ' ', '\n', '\r', '\t' }, 
            StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    /// <summary>
    /// Проверяет похожи ли тексты (учитывает небольшие различия в OCR)
    /// </summary>
    private static bool IsSimilarText(string text1, string text2)
    {
        if (string.IsNullOrEmpty(text1) && string.IsNullOrEmpty(text2))
            return true;
        
        if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2))
            return false;

        // Точное совпадение
        if (text1 == text2)
            return true;

        // Проверяем схожесть (допускаем 10% различий для OCR ошибок)
        var similarity = CalculateSimilarity(text1, text2);
        return similarity > 0.9; // 90% схожести
    }

    /// <summary>
    /// Вычисляет схожесть двух строк (0.0 - 1.0)
    /// </summary>
    private static double CalculateSimilarity(string s1, string s2)
    {
        if (s1 == s2) return 1.0;
        
        int maxLen = Math.Max(s1.Length, s2.Length);
        if (maxLen == 0) return 1.0;
        
        int distance = LevenshteinDistance(s1, s2);
        return 1.0 - (double)distance / maxLen;
    }

    /// <summary>
    /// Вычисляет расстояние Левенштейна между двумя строками
    /// </summary>
    private static int LevenshteinDistance(string s1, string s2)
    {
        int[,] d = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++)
            d[i, 0] = i;
        for (int j = 0; j <= s2.Length; j++)
            d[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                int cost = (s2[j - 1] == s1[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[s1.Length, s2.Length];
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
