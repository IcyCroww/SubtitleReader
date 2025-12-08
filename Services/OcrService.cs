using System;
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
/// OCR сервис - использует Windows OCR (лучше для игровых шрифтов)
/// </summary>
public class OcrService : IDisposable
{
    private readonly ScreenCaptureService _screenCapture;
    private OcrEngine? _ocrEngine;
    private string _currentLanguage = "ru";
    private bool _isDisposed;

    public OcrService()
    {
        _screenCapture = new ScreenCaptureService();
        InitializeEngine();
    }

    private void InitializeEngine()
    {
        try
        {
            // Пробуем русский язык
            var ruLanguage = new Windows.Globalization.Language("ru");
            if (OcrEngine.IsLanguageSupported(ruLanguage))
            {
                _ocrEngine = OcrEngine.TryCreateFromLanguage(ruLanguage);
                _currentLanguage = "ru";
                System.Diagnostics.Debug.WriteLine("[OCR] Windows OCR инициализирован с русским языком");
                return;
            }

            // Пробуем английский
            var enLanguage = new Windows.Globalization.Language("en");
            if (OcrEngine.IsLanguageSupported(enLanguage))
            {
                _ocrEngine = OcrEngine.TryCreateFromLanguage(enLanguage);
                _currentLanguage = "en";
                System.Diagnostics.Debug.WriteLine("[OCR] Windows OCR инициализирован с английским языком");
                return;
            }

            // Используем язык по умолчанию
            _ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
            _currentLanguage = "auto";
            System.Diagnostics.Debug.WriteLine("[OCR] Windows OCR инициализирован с языком по умолчанию");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OCR] Ошибка инициализации: {ex.Message}");
        }
    }

    public bool IsAvailable => _ocrEngine != null;
    public string CurrentLanguage => _currentLanguage;

    /// <summary>
    /// Распознаёт текст в указанной области экрана
    /// </summary>
    public async Task<string> RecognizeRegionAsync(System.Windows.Rect region)
    {
        if (_ocrEngine == null)
        {
            System.Diagnostics.Debug.WriteLine("[OCR] Engine не инициализирован!");
            return string.Empty;
        }

        if (region.Width <= 0 || region.Height <= 0)
        {
            return string.Empty;
        }

        try
        {
            // Захватываем область экрана
            using var screenshot = _screenCapture.CaptureRegion(region);
            if (screenshot == null)
            {
                System.Diagnostics.Debug.WriteLine("[OCR] Не удалось захватить скриншот");
                return string.Empty;
            }

            // Увеличиваем изображение для лучшего распознавания
            using var scaled = ScaleImage(screenshot, 2);

            // Конвертируем в SoftwareBitmap для Windows OCR
            var softwareBitmap = await ConvertToSoftwareBitmapAsync(scaled);
            if (softwareBitmap == null)
            {
                return string.Empty;
            }

            // Распознаём текст
            var result = await _ocrEngine.RecognizeAsync(softwareBitmap);
            
            var text = result.Text?.Trim() ?? string.Empty;
            
            if (!string.IsNullOrEmpty(text))
            {
                System.Diagnostics.Debug.WriteLine($"[OCR] Распознано: {text.Substring(0, Math.Min(50, text.Length))}...");
            }
            
            return text;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OCR] Ошибка: {ex.Message}");
            return string.Empty;
        }
    }

    private Bitmap ScaleImage(Bitmap source, int scale)
    {
        int newWidth = source.Width * scale;
        int newHeight = source.Height * scale;

        var scaled = new Bitmap(newWidth, newHeight);
        using (var graphics = Graphics.FromImage(scaled))
        {
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            graphics.DrawImage(source, 0, 0, newWidth, newHeight);
        }
        return scaled;
    }

    private async Task<SoftwareBitmap?> ConvertToSoftwareBitmapAsync(Bitmap bitmap)
    {
        try
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            ms.Position = 0;

            var randomAccessStream = new InMemoryRandomAccessStream();
            await randomAccessStream.WriteAsync(ms.ToArray().AsBuffer());
            randomAccessStream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8, 
                BitmapAlphaMode.Premultiplied);

            return softwareBitmap;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OCR] Ошибка конвертации: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"[OCR] Ошибка смены языка: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
        }
    }
}
