using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SubtitleReader.Views;

public partial class RegionSelectorWindow : Window
{
    private System.Windows.Point _startPoint;
    private bool _isSelecting;
    private Rect _selectedRegion;

    public Rect SelectedRegion => _selectedRegion;
    public bool RegionSelected { get; private set; }

    public RegionSelectorWindow()
    {
        InitializeComponent();
        
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        KeyDown += OnKeyDown;
    }

    private void OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(SelectionCanvas);
        _isSelecting = true;
        
        SelectionBorder.Visibility = Visibility.Visible;
        SizeInfoBorder.Visibility = Visibility.Visible;
        
        Canvas.SetLeft(SelectionBorder, _startPoint.X);
        Canvas.SetTop(SelectionBorder, _startPoint.Y);
        SelectionBorder.Width = 0;
        SelectionBorder.Height = 0;
        
        CaptureMouse();
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isSelecting)
            return;

        var currentPoint = e.GetPosition(SelectionCanvas);
        
        var x = Math.Min(_startPoint.X, currentPoint.X);
        var y = Math.Min(_startPoint.Y, currentPoint.Y);
        var width = Math.Abs(currentPoint.X - _startPoint.X);
        var height = Math.Abs(currentPoint.Y - _startPoint.Y);

        Canvas.SetLeft(SelectionBorder, x);
        Canvas.SetTop(SelectionBorder, y);
        SelectionBorder.Width = width;
        SelectionBorder.Height = height;

        // Обновляем информацию о размере
        SizeInfoText.Text = $"{(int)width} × {(int)height}";
        Canvas.SetLeft(SizeInfoBorder, x);
        Canvas.SetTop(SizeInfoBorder, y - 30);
        
        if (y - 30 < 0)
        {
            Canvas.SetTop(SizeInfoBorder, y + height + 5);
        }
    }

    private void OnMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_isSelecting)
            return;

        _isSelecting = false;
        ReleaseMouseCapture();

        var currentPoint = e.GetPosition(SelectionCanvas);
        
        var x = Math.Min(_startPoint.X, currentPoint.X);
        var y = Math.Min(_startPoint.Y, currentPoint.Y);
        var width = Math.Abs(currentPoint.X - _startPoint.X);
        var height = Math.Abs(currentPoint.Y - _startPoint.Y);

        // Минимальный размер области
        if (width >= 10 && height >= 10)
        {
            _selectedRegion = new Rect(x, y, width, height);
            RegionSelected = true;
            DialogResult = true;
            Close();
        }
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            RegionSelected = false;
            DialogResult = false;
            Close();
        }
    }
}
