using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace SubtitleReader.Models;

public class TextRegion : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = "Новая область";
    private Rect _bounds;
    private string _text = string.Empty;
    private string _lastRecognizedText = string.Empty;
    private double _readingSpeed = 1.8; // Быстрая скорость чтения
    private bool _isActive = true;
    private bool _autoRead = true; // Авто-чтение включено по умолчанию!
    private int _monitorIntervalMs = 200; // Быстрая проверка каждые 200мс
    private bool _isMonitoring = false;

    public string Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public Rect Bounds
    {
        get => _bounds;
        set => SetField(ref _bounds, value);
    }

    public string Text
    {
        get => _text;
        set => SetField(ref _text, value);
    }

    public string LastRecognizedText
    {
        get => _lastRecognizedText;
        set => SetField(ref _lastRecognizedText, value);
    }

    /// <summary>
    /// Множитель скорости чтения (0.5 - 2.0, где 1.0 = нормальная скорость)
    /// </summary>
    public double ReadingSpeed
    {
        get => _readingSpeed;
        set => SetField(ref _readingSpeed, Math.Clamp(value, 0.5, 2.0));
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetField(ref _isActive, value);
    }

    /// <summary>
    /// Автоматически читать при изменении текста
    /// </summary>
    public bool AutoRead
    {
        get => _autoRead;
        set => SetField(ref _autoRead, value);
    }

    /// <summary>
    /// Интервал мониторинга области в миллисекундах
    /// </summary>
    public int MonitorIntervalMs
    {
        get => _monitorIntervalMs;
        set => SetField(ref _monitorIntervalMs, Math.Max(100, value));
    }

    public bool IsMonitoring
    {
        get => _isMonitoring;
        set => SetField(ref _isMonitoring, value);
    }

    // Для сериализации - храним координаты отдельно
    public double BoundsX
    {
        get => Bounds.X;
        set => Bounds = new Rect(value, Bounds.Y, Bounds.Width, Bounds.Height);
    }

    public double BoundsY
    {
        get => Bounds.Y;
        set => Bounds = new Rect(Bounds.X, value, Bounds.Width, Bounds.Height);
    }

    public double BoundsWidth
    {
        get => Bounds.Width;
        set => Bounds = new Rect(Bounds.X, Bounds.Y, value, Bounds.Height);
    }

    public double BoundsHeight
    {
        get => Bounds.Height;
        set => Bounds = new Rect(Bounds.X, Bounds.Y, Bounds.Width, value);
    }

    public TextRegion Clone()
    {
        return new TextRegion
        {
            Id = Guid.NewGuid().ToString(),
            Name = Name + " (копия)",
            Bounds = Bounds,
            Text = Text,
            ReadingSpeed = ReadingSpeed,
            IsActive = IsActive,
            AutoRead = AutoRead,
            MonitorIntervalMs = MonitorIntervalMs
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
