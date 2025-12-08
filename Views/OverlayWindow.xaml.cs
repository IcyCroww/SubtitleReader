using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SubtitleReader.Models;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;

namespace SubtitleReader.Views;

public partial class OverlayWindow : Window
{
    private readonly Dictionary<string, Border> _regionBorders = new();

    public OverlayWindow()
    {
        InitializeComponent();
    }

    public void UpdateRegions(IEnumerable<TextRegion> regions)
    {
        OverlayCanvas.Children.Clear();
        _regionBorders.Clear();

        foreach (var region in regions)
        {
            if (!region.IsActive)
                continue;

            var border = CreateRegionBorder(region);
            _regionBorders[region.Id] = border;
            OverlayCanvas.Children.Add(border);
        }
    }

    public void HighlightRegion(string regionId, bool highlight)
    {
        if (_regionBorders.TryGetValue(regionId, out var border))
        {
            border.BorderBrush = highlight 
                ? new SolidColorBrush(Color.FromArgb(200, 16, 185, 129))  // Green
                : new SolidColorBrush(Color.FromArgb(150, 124, 58, 237)); // Purple
            
            border.BorderThickness = highlight ? new Thickness(3) : new Thickness(2);
        }
    }

    private Border CreateRegionBorder(TextRegion region)
    {
        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(150, 124, 58, 237)),
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Color.FromArgb(30, 124, 58, 237)),
            CornerRadius = new CornerRadius(4),
            Width = region.Bounds.Width,
            Height = region.Bounds.Height
        };

        // Добавляем название области
        var label = new TextBlock
        {
            Text = region.Name,
            Foreground = Brushes.White,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
            Padding = new Thickness(4, 2, 4, 2),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            VerticalAlignment = System.Windows.VerticalAlignment.Top
        };

        border.Child = label;

        Canvas.SetLeft(border, region.Bounds.X);
        Canvas.SetTop(border, region.Bounds.Y);

        return border;
    }
}
