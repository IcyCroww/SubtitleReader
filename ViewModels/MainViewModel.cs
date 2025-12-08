using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SubtitleReader.Models;
using SubtitleReader.Services;
using SubtitleReader.Views;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace SubtitleReader.ViewModels;

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly TextToSpeechService _ttsService;
    private readonly PresetService _presetService;
    private readonly OcrService _ocrService;
    private readonly RegionMonitorService _monitorService;
    private readonly SettingsService _settingsService;
    
    private TextRegion? _selectedRegion;
    private GamePreset? _currentPreset;
    private AppSettings _settings;
    private string _statusMessage = "Готов к работе | F9: Старт/Стоп | F10: Стоп чтения";
    private bool _isMonitoringActive;
    private bool _isOverlayVisible;
    private OverlayWindow? _overlayWindow;
    private bool _isDisposed;

    private const int MaxHistorySize = 100;

    public MainViewModel()
    {
        _ttsService = new TextToSpeechService();
        _presetService = new PresetService();
        _ocrService = new OcrService();
        _monitorService = new RegionMonitorService(_ocrService, _ttsService);
        _settingsService = new SettingsService();
        _settings = _settingsService.LoadSettings();
        
        // Передаём настройки в монитор для фильтрации
        _monitorService.Settings = _settings;

        Regions = new ObservableCollection<TextRegion>();
        Presets = new ObservableCollection<GamePreset>();
        TextHistory = new ObservableCollection<TextHistoryEntry>();

        // Применяем настройки
        ApplySettings();
        
        // Загружаем пресеты
        LoadPresets();

        // Подписываемся на события
        _monitorService.TextChanged += OnTextChanged;
        _monitorService.Error += OnMonitorError;

        // Инициализируем команды
        InitializeCommands();
    }

    private void InitializeCommands()
    {
        AddRegionCommand = new RelayCommand(_ => AddRegion());
        AddRegionFromScreenCommand = new RelayCommand(_ => AddRegionFromScreen());
        RemoveRegionCommand = new RelayCommand(_ => RemoveRegion(), _ => SelectedRegion != null);
        DuplicateRegionCommand = new RelayCommand(_ => DuplicateRegion(), _ => SelectedRegion != null);
        
        ReadRegionCommand = new RelayCommand(_ => ReadRegion(), _ => SelectedRegion != null);
        ReadAllRegionsCommand = new RelayCommand(_ => ReadAllRegions(), _ => Regions.Any(r => r.IsActive));
        StopReadingCommand = new RelayCommand(_ => StopReading());
        
        CaptureRegionTextCommand = new RelayCommand(_ => CaptureRegionText(), _ => SelectedRegion != null);
        
        StartMonitoringCommand = new RelayCommand(_ => StartMonitoring(), _ => Regions.Any(r => r.IsActive));
        StopMonitoringCommand = new RelayCommand(_ => StopMonitoring());
        StartRegionMonitoringCommand = new RelayCommand(_ => StartRegionMonitoring(), _ => SelectedRegion != null);
        StopRegionMonitoringCommand = new RelayCommand(_ => StopRegionMonitoring(), _ => SelectedRegion?.IsMonitoring == true);
        
        SavePresetCommand = new RelayCommand(_ => SaveCurrentPreset(), _ => Regions.Count > 0);
        LoadPresetCommand = new RelayCommand(_ => LoadPreset(), _ => CurrentPreset != null);
        DeletePresetCommand = new RelayCommand(_ => DeletePreset(), _ => CurrentPreset != null);
        EditPresetCommand = new RelayCommand(_ => EditPreset(), _ => CurrentPreset != null);
        
        OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
        ClearAllRegionsCommand = new RelayCommand(_ => ClearAllRegions(), _ => Regions.Count > 0);
        ToggleOverlayCommand = new RelayCommand(_ => ToggleOverlay());
        ReselectRegionCommand = new RelayCommand(_ => ReselectRegion(), _ => SelectedRegion != null);
        CopyTextCommand = new RelayCommand(_ => CopyText(), _ => SelectedRegion != null && !string.IsNullOrEmpty(SelectedRegion.Text));
        ClearHistoryCommand = new RelayCommand(_ => ClearHistory(), _ => TextHistory.Count > 0);
        ExportHistoryCommand = new RelayCommand(_ => ExportHistory(), _ => TextHistory.Count > 0);
        AutoDetectRegionsCommand = new RelayCommand(_ => AutoDetectRegions());
    }

    #region Properties

    public ObservableCollection<TextRegion> Regions { get; }
    public ObservableCollection<GamePreset> Presets { get; }
    public ObservableCollection<TextHistoryEntry> TextHistory { get; }

    public TextRegion? SelectedRegion
    {
        get => _selectedRegion;
        set
        {
            _selectedRegion = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public GamePreset? CurrentPreset
    {
        get => _currentPreset;
        set
        {
            _currentPreset = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public AppSettings Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public bool IsMonitoringActive
    {
        get => _isMonitoringActive;
        set
        {
            _isMonitoringActive = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsOcrAvailable => _ocrService.IsAvailable;
    public string OcrLanguage => _ocrService.CurrentLanguage;

    public bool IsOverlayVisible
    {
        get => _isOverlayVisible;
        set
        {
            _isOverlayVisible = value;
            OnPropertyChanged();
            UpdateOverlay();
        }
    }

    #endregion

    #region Commands

    public ICommand AddRegionCommand { get; private set; } = null!;
    public ICommand AddRegionFromScreenCommand { get; private set; } = null!;
    public ICommand RemoveRegionCommand { get; private set; } = null!;
    public ICommand DuplicateRegionCommand { get; private set; } = null!;
    
    public ICommand ReadRegionCommand { get; private set; } = null!;
    public ICommand ReadAllRegionsCommand { get; private set; } = null!;
    public ICommand StopReadingCommand { get; private set; } = null!;
    
    public ICommand CaptureRegionTextCommand { get; private set; } = null!;
    
    public ICommand StartMonitoringCommand { get; private set; } = null!;
    public ICommand StopMonitoringCommand { get; private set; } = null!;
    public ICommand StartRegionMonitoringCommand { get; private set; } = null!;
    public ICommand StopRegionMonitoringCommand { get; private set; } = null!;
    
    public ICommand SavePresetCommand { get; private set; } = null!;
    public ICommand LoadPresetCommand { get; private set; } = null!;
    public ICommand DeletePresetCommand { get; private set; } = null!;
    public ICommand EditPresetCommand { get; private set; } = null!;
    
    public ICommand OpenSettingsCommand { get; private set; } = null!;
    public ICommand ClearAllRegionsCommand { get; private set; } = null!;
    public ICommand ToggleOverlayCommand { get; private set; } = null!;
    public ICommand ReselectRegionCommand { get; private set; } = null!;
    public ICommand CopyTextCommand { get; private set; } = null!;
    public ICommand ClearHistoryCommand { get; private set; } = null!;
    public ICommand ExportHistoryCommand { get; private set; } = null!;
    public ICommand AutoDetectRegionsCommand { get; private set; } = null!;

    #endregion

    #region Region Methods

    private void AddRegion()
    {
        var region = new TextRegion
        {
            Name = $"Область {Regions.Count + 1}",
            Bounds = new Rect(100, 100, 300, 100),
            ReadingSpeed = _settings.DefaultReadingSpeed
        };
        Regions.Add(region);
        SelectedRegion = region;
        StatusMessage = $"Добавлена область: {region.Name}";
    }

    private void AddRegionFromScreen()
    {
        var selector = new RegionSelectorWindow();
        
        if (selector.ShowDialog() == true && selector.RegionSelected)
        {
            var region = new TextRegion
            {
                Name = $"Область {Regions.Count + 1}",
                Bounds = selector.SelectedRegion,
                ReadingSpeed = _settings.DefaultReadingSpeed
            };
            Regions.Add(region);
            SelectedRegion = region;
            StatusMessage = $"Область выделена: {(int)region.Bounds.Width}×{(int)region.Bounds.Height}";
            
            // Сразу распознаём текст
            CaptureRegionText();
        }
    }

    private void RemoveRegion()
    {
        if (SelectedRegion != null)
        {
            _monitorService.StopMonitoring(SelectedRegion);
            var name = SelectedRegion.Name;
            Regions.Remove(SelectedRegion);
            SelectedRegion = Regions.FirstOrDefault();
            StatusMessage = $"Удалена область: {name}";
        }
    }

    private void DuplicateRegion()
    {
        if (SelectedRegion != null)
        {
            var clone = SelectedRegion.Clone();
            Regions.Add(clone);
            SelectedRegion = clone;
            StatusMessage = $"Создана копия: {clone.Name}";
        }
    }

    private void ClearAllRegions()
    {
        if (MessageBox.Show("Удалить все области?", "Подтверждение", 
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _monitorService.StopAllMonitoring();
            Regions.Clear();
            SelectedRegion = null;
            StatusMessage = "Все области удалены";
        }
    }

    private void ReselectRegion()
    {
        if (SelectedRegion == null)
            return;

        var selector = new RegionSelectorWindow();
        
        if (selector.ShowDialog() == true && selector.RegionSelected)
        {
            SelectedRegion.Bounds = selector.SelectedRegion;
            StatusMessage = $"Область изменена: {(int)SelectedRegion.Bounds.Width}×{(int)SelectedRegion.Bounds.Height}";
            RefreshOverlay();
            
            // Сразу распознаём текст
            CaptureRegionText();
        }
    }

    #endregion

    #region Reading Methods

    private async void ReadRegion()
    {
        if (SelectedRegion != null && !string.IsNullOrWhiteSpace(SelectedRegion.Text))
        {
            StatusMessage = $"Читаю: {SelectedRegion.Name}";
            await _ttsService.SpeakAsync(SelectedRegion);
            StatusMessage = "Готов к работе";
        }
    }

    private async void ReadAllRegions()
    {
        var activeRegions = Regions.Where(r => r.IsActive && !string.IsNullOrWhiteSpace(r.Text)).ToList();
        
        foreach (var region in activeRegions)
        {
            StatusMessage = $"Читаю: {region.Name}";
            await _ttsService.SpeakAsync(region);
        }
        
        StatusMessage = "Готов к работе";
    }

    private void StopReading()
    {
        _ttsService.Stop();
        StatusMessage = "Чтение остановлено";
    }

    private void CopyText()
    {
        if (SelectedRegion != null && !string.IsNullOrEmpty(SelectedRegion.Text))
        {
            try
            {
                System.Windows.Clipboard.SetText(SelectedRegion.Text);
                StatusMessage = "Текст скопирован в буфер обмена";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка копирования: {ex.Message}";
            }
        }
    }

    private void ClearHistory()
    {
        TextHistory.Clear();
        StatusMessage = "История очищена";
    }

    private void ExportHistory()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "SRT файл|*.srt|Текстовый файл|*.txt",
                DefaultExt = ".srt",
                FileName = $"subtitles_{DateTime.Now:yyyy-MM-dd_HH-mm}"
            };

            if (dialog.ShowDialog() == true)
            {
                using var writer = new StreamWriter(dialog.FileName);
                
                if (dialog.FileName.EndsWith(".srt"))
                {
                    // Формат SRT
                    var entries = TextHistory.Reverse().ToList();
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var entry = entries[i];
                        var startTime = entry.Timestamp;
                        var endTime = startTime.AddSeconds(3);
                        
                        writer.WriteLine(i + 1);
                        writer.WriteLine($"{startTime:HH:mm:ss,fff} --> {endTime:HH:mm:ss,fff}");
                        writer.WriteLine(entry.Text);
                        writer.WriteLine();
                    }
                }
                else
                {
                    // Простой текст
                    foreach (var entry in TextHistory.Reverse())
                    {
                        writer.WriteLine($"[{entry.FormattedTime}] [{entry.RegionName}] {entry.Text}");
                    }
                }
                
                StatusMessage = $"История экспортирована: {Path.GetFileName(dialog.FileName)}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка экспорта: {ex.Message}";
        }
    }

    private void AutoDetectRegions()
    {
        try
        {
            StatusMessage = "Поиск областей с субтитрами...";
            
            var detector = new SubtitleDetectorService();
            
            // Сначала пробуем автоопределение
            var detected = detector.DetectTextRegions();
            
            if (detected.Count == 0)
            {
                // Если не нашли, предлагаем стандартные области
                detected = detector.GetDefaultSubtitleRegions();
                StatusMessage = "Добавлены стандартные области для субтитров";
            }
            else
            {
                StatusMessage = $"Найдено {detected.Count} областей с текстом";
            }

            foreach (var rect in detected.Take(4)) // Максимум 4 области
            {
                var region = new TextRegion
                {
                    Name = $"Авто-область {Regions.Count + 1}",
                    Bounds = rect,
                    ReadingSpeed = _settings.DefaultReadingSpeed
                };
                Regions.Add(region);
            }

            SelectedRegion = Regions.FirstOrDefault();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка автоопределения: {ex.Message}";
        }
    }

    #endregion

    #region OCR Methods

    private async void CaptureRegionText()
    {
        if (SelectedRegion == null)
            return;

        if (!_ocrService.IsAvailable)
        {
            StatusMessage = "OCR недоступен. Проверьте наличие языковых файлов в папке tessdata";
            return;
        }

        StatusMessage = "Распознавание текста...";
        
        var text = await _ocrService.RecognizeRegionAsync(SelectedRegion.Bounds);
        
        if (!string.IsNullOrWhiteSpace(text))
        {
            SelectedRegion.Text = text;
            SelectedRegion.LastRecognizedText = text;
            StatusMessage = $"Распознано {text.Length} символов";
        }
        else
        {
            StatusMessage = "Текст не распознан";
        }
    }

    #endregion

    #region Monitoring Methods

    private void StartMonitoring()
    {
        if (!_ocrService.IsAvailable)
        {
            StatusMessage = "OCR недоступен! Проверьте настройки Windows OCR";
            return;
        }

        var activeRegions = Regions.Where(r => r.IsActive).ToList();
        
        if (activeRegions.Count == 0)
        {
            StatusMessage = "Нет активных областей для мониторинга!";
            return;
        }

        foreach (var region in activeRegions)
        {
            // Убеждаемся что AutoRead включен
            if (!region.AutoRead)
            {
                region.AutoRead = true;
            }
            _monitorService.StartMonitoring(region);
        }
        
        IsMonitoringActive = true;
        StatusMessage = $"Мониторинг запущен для {activeRegions.Count} областей (OCR: {_ocrService.CurrentLanguage})";
    }

    private void StopMonitoring()
    {
        _monitorService.StopAllMonitoring();
        IsMonitoringActive = false;
        StatusMessage = "Мониторинг остановлен";
    }

    private void StartRegionMonitoring()
    {
        if (SelectedRegion != null)
        {
            _monitorService.StartMonitoring(SelectedRegion);
            StatusMessage = $"Мониторинг области: {SelectedRegion.Name}";
        }
    }

    private void StopRegionMonitoring()
    {
        if (SelectedRegion != null)
        {
            _monitorService.StopMonitoring(SelectedRegion);
            StatusMessage = $"Мониторинг остановлен: {SelectedRegion.Name}";
        }
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            StatusMessage = $"Новый текст в области: {e.Region.Name}";
            
            // Добавляем в историю
            var entry = new TextHistoryEntry
            {
                RegionName = e.Region.Name,
                Text = e.NewText,
                WasRead = e.Region.AutoRead
            };
            TextHistory.Insert(0, entry);
            
            // Ограничиваем размер истории
            while (TextHistory.Count > MaxHistorySize)
                TextHistory.RemoveAt(TextHistory.Count - 1);
        });
    }

    private void OnMonitorError(object? sender, RegionErrorEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            StatusMessage = $"Ошибка в области {e.Region.Name}: {e.Exception.Message}";
        });
    }

    #endregion

    #region Preset Methods

    private void SaveCurrentPreset()
    {
        var dialog = new PresetEditDialog();
        dialog.Owner = Application.Current.MainWindow;
        
        if (dialog.ShowDialog() == true)
        {
            var preset = new GamePreset
            {
                Name = dialog.PresetName,
                Description = dialog.PresetDescription,
                Regions = Regions.ToList(),
                ModifiedAt = DateTime.Now
            };
            
            Presets.Add(preset);
            _presetService.SavePreset(preset);
            CurrentPreset = preset;
            StatusMessage = $"Заготовка сохранена: {preset.Name}";
        }
    }

    private void LoadPreset()
    {
        if (CurrentPreset != null)
        {
            _monitorService.StopAllMonitoring();
            Regions.Clear();
            
            foreach (var region in CurrentPreset.Regions)
            {
                Regions.Add(region);
            }
            
            SelectedRegion = Regions.FirstOrDefault();
            StatusMessage = $"Загружена заготовка: {CurrentPreset.Name}";
        }
    }

    private void DeletePreset()
    {
        if (CurrentPreset != null)
        {
            if (MessageBox.Show($"Удалить заготовку \"{CurrentPreset.Name}\"?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var name = CurrentPreset.Name;
                _presetService.DeletePreset(CurrentPreset.Id);
                Presets.Remove(CurrentPreset);
                CurrentPreset = Presets.FirstOrDefault();
                StatusMessage = $"Заготовка удалена: {name}";
            }
        }
    }

    private void EditPreset()
    {
        if (CurrentPreset != null)
        {
            var dialog = new PresetEditDialog(CurrentPreset);
            dialog.Owner = Application.Current.MainWindow;
            
            if (dialog.ShowDialog() == true)
            {
                CurrentPreset.Name = dialog.PresetName;
                CurrentPreset.Description = dialog.PresetDescription;
                CurrentPreset.ModifiedAt = DateTime.Now;
                _presetService.SavePreset(CurrentPreset);
                StatusMessage = $"Заготовка обновлена: {CurrentPreset.Name}";
            }
        }
    }

    private void LoadPresets()
    {
        // Загружаем пользовательские пресеты
        var presets = _presetService.LoadPresets();
        foreach (var preset in presets)
        {
            Presets.Add(preset);
        }

        // Добавляем встроенные пресеты для игр (если нет пользовательских)
        if (Presets.Count == 0)
        {
            var builtIn = GamePresetsService.GetBuiltInPresets();
            foreach (var preset in builtIn)
            {
                Presets.Add(preset);
            }
        }
    }

    #endregion

    #region Overlay Methods

    private void ToggleOverlay()
    {
        IsOverlayVisible = !IsOverlayVisible;
    }

    private void UpdateOverlay()
    {
        if (_isOverlayVisible)
        {
            if (_overlayWindow == null)
            {
                _overlayWindow = new OverlayWindow();
            }
            _overlayWindow.UpdateRegions(Regions);
            _overlayWindow.Show();
            StatusMessage = "Overlay включён";
        }
        else
        {
            _overlayWindow?.Hide();
            StatusMessage = "Overlay выключен";
        }
    }

    public void RefreshOverlay()
    {
        if (_isOverlayVisible && _overlayWindow != null)
        {
            _overlayWindow.UpdateRegions(Regions);
        }
    }

    #endregion

    #region Settings Methods

    private void OpenSettings()
    {
        var dialog = new SettingsWindow(_settings, _ttsService);
        dialog.Owner = Application.Current.MainWindow;
        
        if (dialog.ShowDialog() == true)
        {
            _settings = dialog.Settings;
            _settingsService.SaveSettings(_settings);
            ApplySettings();
            StatusMessage = "Настройки сохранены";
        }
    }

    private void ApplySettings()
    {
        _ttsService.Volume = _settings.Volume;
        
        if (!string.IsNullOrEmpty(_settings.SelectedVoice))
        {
            _ttsService.SetVoice(_settings.SelectedVoice);
        }
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _overlayWindow?.Close();
            _monitorService.Dispose();
            _ttsService.Dispose();
            _ocrService.Dispose();
            _isDisposed = true;
        }
    }

    #endregion
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
