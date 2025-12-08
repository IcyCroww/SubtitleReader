using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SubtitleReader.Models;

public class AppSettings : INotifyPropertyChanged
{
    private string _selectedVoice = string.Empty;
    private int _volume = 100;
    private double _defaultReadingSpeed = 1.5; // Увеличена скорость по умолчанию
    private string _ocrLanguage = "rus+eng";
    private bool _showOverlayOnStartup = false;
    private string _hotKeyStartMonitoring = "F9";
    private string _hotKeyStopMonitoring = "F10";
    private string _hotKeyReadSelected = "F11";
    private bool _minimizeToTray = true;
    private bool _startMinimized = false;

    public string SelectedVoice
    {
        get => _selectedVoice;
        set => SetField(ref _selectedVoice, value);
    }

    public int Volume
    {
        get => _volume;
        set => SetField(ref _volume, Math.Clamp(value, 0, 100));
    }

    public double DefaultReadingSpeed
    {
        get => _defaultReadingSpeed;
        set => SetField(ref _defaultReadingSpeed, Math.Clamp(value, 0.5, 2.0));
    }

    public string OcrLanguage
    {
        get => _ocrLanguage;
        set => SetField(ref _ocrLanguage, value);
    }

    public bool ShowOverlayOnStartup
    {
        get => _showOverlayOnStartup;
        set => SetField(ref _showOverlayOnStartup, value);
    }

    public string HotKeyStartMonitoring
    {
        get => _hotKeyStartMonitoring;
        set => SetField(ref _hotKeyStartMonitoring, value);
    }

    public string HotKeyStopMonitoring
    {
        get => _hotKeyStopMonitoring;
        set => SetField(ref _hotKeyStopMonitoring, value);
    }

    public string HotKeyReadSelected
    {
        get => _hotKeyReadSelected;
        set => SetField(ref _hotKeyReadSelected, value);
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set => SetField(ref _minimizeToTray, value);
    }

    public bool StartMinimized
    {
        get => _startMinimized;
        set => SetField(ref _startMinimized, value);
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
