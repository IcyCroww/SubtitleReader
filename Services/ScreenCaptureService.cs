using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;

namespace SubtitleReader.Services;

/// <summary>
/// Сервис захвата экрана
/// </summary>
public sealed class ScreenCaptureService
{

    /// <summary>
    /// Захватывает область экрана
    /// </summary>
    public Bitmap? CaptureRegion(Rect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            return null;

        try
        {
            var x = (int)region.X;
            var y = (int)region.Y;
            var width = (int)region.Width;
            var height = (int)region.Height;

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppRgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height));
            return bitmap;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Capture] Ошибка: {ex.Message}");
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
            var width = (int)SystemParameters.VirtualScreenWidth;
            var height = (int)SystemParameters.VirtualScreenHeight;
            var left = (int)SystemParameters.VirtualScreenLeft;
            var top = (int)SystemParameters.VirtualScreenTop;

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppRgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(left, top, 0, 0, new System.Drawing.Size(width, height));
            return bitmap;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Capture] Ошибка: {ex.Message}");
            return null;
        }
    }
}
