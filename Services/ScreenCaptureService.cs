using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;

namespace SubtitleReader.Services;

public class ScreenCaptureService
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
        IntPtr hdcSource, int xSrc, int ySrc, int rop);

    private const int SRCCOPY = 0x00CC0020;

    /// <summary>
    /// Захватывает область экрана
    /// </summary>
    public Bitmap? CaptureRegion(Rect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            return null;

        try
        {
            int x = (int)region.X;
            int y = (int)region.Y;
            int width = (int)region.Width;
            int height = (int)region.Height;

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);
            }

            return bitmap;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка захвата экрана: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Захватывает весь экран
    /// </summary>
    public Bitmap? CaptureFullScreen()
    {
        try
        {
            var screenWidth = (int)SystemParameters.VirtualScreenWidth;
            var screenHeight = (int)SystemParameters.VirtualScreenHeight;
            var screenLeft = (int)SystemParameters.VirtualScreenLeft;
            var screenTop = (int)SystemParameters.VirtualScreenTop;

            var bitmap = new Bitmap(screenWidth, screenHeight, PixelFormat.Format32bppArgb);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(screenLeft, screenTop, 0, 0, 
                    new System.Drawing.Size(screenWidth, screenHeight), CopyPixelOperation.SourceCopy);
            }

            return bitmap;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка захвата экрана: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Предобработка изображения для улучшения OCR
    /// </summary>
    public Bitmap PreprocessForOcr(Bitmap source)
    {
        // Увеличиваем изображение для лучшего распознавания
        int newWidth = source.Width * 2;
        int newHeight = source.Height * 2;

        var scaled = new Bitmap(newWidth, newHeight);
        using (var graphics = Graphics.FromImage(scaled))
        {
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(source, 0, 0, newWidth, newHeight);
        }

        // Конвертируем в grayscale и увеличиваем контраст
        var processed = new Bitmap(newWidth, newHeight);
        
        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                var pixel = scaled.GetPixel(x, y);
                int gray = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                
                // Увеличиваем контраст
                gray = (int)((gray - 128) * 1.5 + 128);
                gray = Math.Clamp(gray, 0, 255);
                
                processed.SetPixel(x, y, Color.FromArgb(gray, gray, gray));
            }
        }

        scaled.Dispose();
        return processed;
    }
}
