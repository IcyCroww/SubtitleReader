using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows;

namespace SubtitleReader.Services;

/// <summary>
/// Сервис автоматического определения области субтитров
/// </summary>
public sealed class SubtitleDetectorService
{
    private readonly ScreenCaptureService _screenCapture = new();

    /// <summary>
    /// Ищет области с текстом на экране (обычно субтитры внизу)
    /// </summary>
    public List<Rect> DetectTextRegions()
    {
        var regions = new List<Rect>();

        try
        {
            using var screenshot = _screenCapture.CaptureFullScreen();
            if (screenshot == null)
                return regions;

            // Анализируем нижнюю часть экрана (там обычно субтитры)
            var screenHeight = screenshot.Height;
            var screenWidth = screenshot.Width;

            // Проверяем нижние 30% экрана
            var bottomRegion = new Rectangle(0, (int)(screenHeight * 0.7), screenWidth, (int)(screenHeight * 0.3));
            
            var textAreas = FindTextAreas(screenshot, bottomRegion);
            regions.AddRange(textAreas);

            // Также проверяем верхнюю часть (для некоторых игр)
            var topRegion = new Rectangle(0, 0, screenWidth, (int)(screenHeight * 0.15));
            var topAreas = FindTextAreas(screenshot, topRegion);
            regions.AddRange(topAreas);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Detector] Ошибка: {ex.Message}");
        }

        return regions;
    }

    /// <summary>
    /// Ищет области с контрастным текстом
    /// </summary>
    private List<Rect> FindTextAreas(Bitmap image, Rectangle searchArea)
    {
        var areas = new List<Rect>();

        // Упрощённый алгоритм: ищем горизонтальные полосы с высоким контрастом
        var contrastRows = new List<int>();

        for (int y = searchArea.Top; y < searchArea.Bottom; y += 5)
        {
            var rowContrast = CalculateRowContrast(image, y, searchArea.Left, searchArea.Right);
            if (rowContrast > 50) // Порог контраста
            {
                contrastRows.Add(y);
            }
        }

        // Группируем соседние строки в области
        if (contrastRows.Count > 0)
        {
            var groups = GroupConsecutiveRows(contrastRows, 20);
            
            foreach (var group in groups)
            {
                if (group.Count >= 2) // Минимум 2 строки
                {
                    var minY = group.Min() - 10;
                    var maxY = group.Max() + 30;
                    
                    // Находим горизонтальные границы текста
                    var (left, right) = FindHorizontalBounds(image, minY, maxY, searchArea);
                    
                    if (right - left > 100) // Минимальная ширина
                    {
                        areas.Add(new Rect(left, minY, right - left, maxY - minY));
                    }
                }
            }
        }

        return areas;
    }

    private double CalculateRowContrast(Bitmap image, int y, int left, int right)
    {
        if (y < 0 || y >= image.Height)
            return 0;

        var values = new List<int>();
        var step = Math.Max(1, (right - left) / 100);

        for (int x = left; x < right && x < image.Width; x += step)
        {
            var pixel = image.GetPixel(x, y);
            var gray = (pixel.R + pixel.G + pixel.B) / 3;
            values.Add(gray);
        }

        if (values.Count < 2)
            return 0;

        // Вычисляем стандартное отклонение как меру контраста
        var avg = values.Average();
        var variance = values.Sum(v => Math.Pow(v - avg, 2)) / values.Count;
        return Math.Sqrt(variance);
    }

    private List<List<int>> GroupConsecutiveRows(List<int> rows, int maxGap)
    {
        var groups = new List<List<int>>();
        if (rows.Count == 0)
            return groups;

        var currentGroup = new List<int> { rows[0] };

        for (int i = 1; i < rows.Count; i++)
        {
            if (rows[i] - rows[i - 1] <= maxGap)
            {
                currentGroup.Add(rows[i]);
            }
            else
            {
                groups.Add(currentGroup);
                currentGroup = new List<int> { rows[i] };
            }
        }

        groups.Add(currentGroup);
        return groups;
    }

    private (int left, int right) FindHorizontalBounds(Bitmap image, int minY, int maxY, Rectangle searchArea)
    {
        var left = searchArea.Left;
        var right = searchArea.Right;

        // Ищем левую границу
        for (int x = searchArea.Left; x < searchArea.Right; x += 10)
        {
            var hasContent = false;
            for (int y = minY; y < maxY && y < image.Height; y += 5)
            {
                if (x < image.Width)
                {
                    var pixel = image.GetPixel(x, y);
                    var brightness = (pixel.R + pixel.G + pixel.B) / 3;
                    if (brightness > 200 || brightness < 50) // Светлый или тёмный текст
                    {
                        hasContent = true;
                        break;
                    }
                }
            }
            if (hasContent)
            {
                left = Math.Max(searchArea.Left, x - 20);
                break;
            }
        }

        // Ищем правую границу
        for (int x = searchArea.Right - 1; x > left; x -= 10)
        {
            var hasContent = false;
            for (int y = minY; y < maxY && y < image.Height; y += 5)
            {
                if (x < image.Width && x >= 0)
                {
                    var pixel = image.GetPixel(x, y);
                    var brightness = (pixel.R + pixel.G + pixel.B) / 3;
                    if (brightness > 200 || brightness < 50)
                    {
                        hasContent = true;
                        break;
                    }
                }
            }
            if (hasContent)
            {
                right = Math.Min(searchArea.Right, x + 20);
                break;
            }
        }

        return (left, right);
    }

    /// <summary>
    /// Предлагает стандартные области для субтитров
    /// </summary>
    public List<Rect> GetDefaultSubtitleRegions()
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        return new List<Rect>
        {
            // Нижняя центральная область (классические субтитры)
            new Rect(screenWidth * 0.1, screenHeight * 0.85, screenWidth * 0.8, screenHeight * 0.12),
            
            // Верхняя область (для некоторых игр)
            new Rect(screenWidth * 0.1, screenHeight * 0.02, screenWidth * 0.8, screenHeight * 0.08),
            
            // Левый нижний угол (диалоги в RPG)
            new Rect(screenWidth * 0.02, screenHeight * 0.7, screenWidth * 0.4, screenHeight * 0.25),
            
            // Центр экрана (уведомления)
            new Rect(screenWidth * 0.25, screenHeight * 0.4, screenWidth * 0.5, screenHeight * 0.2)
        };
    }
}
