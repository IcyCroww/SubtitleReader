using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace SubtitleReader.Services;

/// <summary>
/// OCR сервис - использует Windows OCR (оптимизирован для игровых шрифтов)
/// </summary>
public sealed class OcrService : IDisposable
{
    private readonly ScreenCaptureService _screenCapture = new();
    private OcrEngine? _ocrEngine;
    private string _currentLanguage = "ru";

    private const int ScaleFactor = 2;

    public OcrService()
    {
        InitializeEngine();
    }

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
    /// Распознаёт текст в указанной области экрана
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

            using var scaled = ScaleImage(screenshot, ScaleFactor);
            using var softwareBitmap = await ConvertToSoftwareBitmapAsync(scaled);
            
            if (softwareBitmap == null)
                return string.Empty;

            var result = await _ocrEngine.RecognizeAsync(softwareBitmap);
            return result.Text?.Trim() ?? string.Empty;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OCR] Ошибка: {ex.Message}");
            return string.Empty;
        }
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

    public void Dispose() { }
}
