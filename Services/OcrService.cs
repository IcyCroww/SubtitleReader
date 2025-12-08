using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace SubtitleReader.Services;

/// <summary>
/// OCR сервис с кэшированием - не распознаёт если изображение не изменилось
/// </summary>
public sealed class OcrService : IDisposable
{
    private readonly ScreenCaptureService _screenCapture = new();
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private OcrEngine? _ocrEngine;
    private string _currentLanguage = "ru";

    private const int ScaleFactor = 2;
    private const int MaxCacheSize = 50;
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromSeconds(30);

    public OcrService()
    {
        InitializeEngine();
    }

    private record CacheEntry(string Text, DateTime CreatedAt, byte[] ImageHash);

    private void InitializeEngine()
    {
        try
        {
            // Приоритет языков: русский -> английский -> системный
            var languages = new[] { "ru", "en" };
            
            foreach (var lang in languages)
            {
                var language = new Windows.Globalization.Language(lang);
                if (OcrEngine.IsLanguageSupported(language))
                {
                    _ocrEngine = OcrEngine.TryCreateFromLanguage(language);
                    _currentLanguage = lang;
                    Debug.WriteLine($"[OCR] Инициализирован: {lang}");
                    return;
                }
            }

            _ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
            _currentLanguage = "auto";
            Debug.WriteLine("[OCR] Инициализирован: системный язык");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OCR] Ошибка инициализации: {ex.Message}");
        }
    }

    public bool IsAvailable => _ocrEngine != null;
    public string CurrentLanguage => _currentLanguage;

    /// <summary>
    /// Распознаёт текст в указанной области экрана (с кэшированием)
    /// </summary>
    public async Task<string> RecognizeRegionAsync(System.Windows.Rect region)
    {
        if (_ocrEngine == null || region.Width <= 0 || region.Height <= 0)
            return string.Empty;

        try
        {
            using var screenshot = _screenCapture.CaptureRegion(region);
            if (screenshot == null)
                return string.Empty;

            // Вычисляем хэш изображения для кэширования
            var imageHash = ComputeImageHash(screenshot);
            var cacheKey = $"{region.X}_{region.Y}_{region.Width}_{region.Height}";

            // Проверяем кэш
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                if (cached.ImageHash.AsSpan().SequenceEqual(imageHash) && 
                    DateTime.Now - cached.CreatedAt < CacheExpiry)
                {
                    return cached.Text; // Изображение не изменилось
                }
            }

            // Распознаём текст
            using var scaled = ScaleImage(screenshot, ScaleFactor);
            using var softwareBitmap = await ConvertToSoftwareBitmapAsync(scaled);
            
            if (softwareBitmap == null)
                return string.Empty;

            var result = await _ocrEngine.RecognizeAsync(softwareBitmap);
            var text = result.Text?.Trim() ?? string.Empty;

            // Сохраняем в кэш
            _cache[cacheKey] = new CacheEntry(text, DateTime.Now, imageHash);
            CleanupCache();

            return text;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OCR] Ошибка: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Быстрый хэш изображения (сэмплирование пикселей)
    /// </summary>
    private static byte[] ComputeImageHash(Bitmap bitmap)
    {
        // Берём сэмпл пикселей для быстрого сравнения
        var samples = new byte[64];
        var stepX = Math.Max(1, bitmap.Width / 8);
        var stepY = Math.Max(1, bitmap.Height / 8);
        var idx = 0;

        for (var y = 0; y < 8 && y * stepY < bitmap.Height; y++)
        {
            for (var x = 0; x < 8 && x * stepX < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x * stepX, y * stepY);
                samples[idx++] = (byte)((pixel.R + pixel.G + pixel.B) / 3);
            }
        }

        return samples;
    }

    /// <summary>
    /// Очистка старых записей кэша
    /// </summary>
    private void CleanupCache()
    {
        if (_cache.Count <= MaxCacheSize) return;

        var keysToRemove = _cache
            .Where(x => DateTime.Now - x.Value.CreatedAt > CacheExpiry)
            .Select(x => x.Key)
            .ToList();

        foreach (var key in keysToRemove)
            _cache.TryRemove(key, out _);
    }

    private static Bitmap ScaleImage(Bitmap source, int scale)
    {
        var newWidth = source.Width * scale;
        var newHeight = source.Height * scale;

        var scaled = new Bitmap(newWidth, newHeight);
        using var graphics = Graphics.FromImage(scaled);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(source, 0, 0, newWidth, newHeight);
        return scaled;
    }

    private static async Task<SoftwareBitmap?> ConvertToSoftwareBitmapAsync(Bitmap bitmap)
    {
        try
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Bmp); // BMP быстрее чем PNG
            
            var buffer = ms.ToArray().AsBuffer();
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(buffer);
            stream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(stream);
            return await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8, 
                BitmapAlphaMode.Premultiplied);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OCR] Ошибка конвертации: {ex.Message}");
            return null;
        }
    }

    public void SetLanguage(string language)
    {
        try
        {
            var lang = new Windows.Globalization.Language(language);
            if (OcrEngine.IsLanguageSupported(lang))
            {
                _ocrEngine = OcrEngine.TryCreateFromLanguage(lang);
                _currentLanguage = language;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OCR] Ошибка смены языка: {ex.Message}");
        }
    }

    public void ClearCache() => _cache.Clear();

    public void Dispose() => _cache.Clear();
}
