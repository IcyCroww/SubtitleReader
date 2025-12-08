using System.Windows;
using SubtitleReader.Models;
using SubtitleReader.Services;

namespace SubtitleReader.Views;

public partial class SettingsWindow : Window
{
    private readonly TextToSpeechService _ttsService;
    private readonly AppSettings _settings;

    public AppSettings Settings => _settings;

    public SettingsWindow(AppSettings settings, TextToSpeechService ttsService)
    {
        InitializeComponent();
        
        _settings = settings;
        _ttsService = ttsService;
        
        LoadVoices();
        LoadSettings();
        
        VolumeSlider.ValueChanged += (s, e) => VolumeText.Text = $"{(int)VolumeSlider.Value}%";
        SpeedSlider.ValueChanged += (s, e) => SpeedText.Text = $"{SpeedSlider.Value:F1}x";
    }

    private void LoadVoices()
    {
        var voices = _ttsService.GetAvailableVoices();
        VoiceComboBox.Items.Clear();
        
        foreach (var voice in voices)
        {
            if (voice.Enabled)
            {
                VoiceComboBox.Items.Add(voice.VoiceInfo.Name);
            }
        }

        if (VoiceComboBox.Items.Count > 0)
        {
            var currentVoice = _ttsService.GetCurrentVoice();
            var index = VoiceComboBox.Items.IndexOf(currentVoice);
            VoiceComboBox.SelectedIndex = index >= 0 ? index : 0;
        }
    }

    private void LoadSettings()
    {
        if (!string.IsNullOrEmpty(_settings.SelectedVoice) && 
            VoiceComboBox.Items.Contains(_settings.SelectedVoice))
        {
            VoiceComboBox.SelectedItem = _settings.SelectedVoice;
        }
        
        VolumeSlider.Value = _settings.Volume;
        VolumeText.Text = $"{_settings.Volume}%";
        
        SpeedSlider.Value = _settings.DefaultReadingSpeed;
        SpeedText.Text = $"{_settings.DefaultReadingSpeed:F1}x";
        
        HotkeyStartTextBox.Text = _settings.HotKeyStartMonitoring;
        HotkeyStopTextBox.Text = _settings.HotKeyStopMonitoring;
        HotkeyReadTextBox.Text = _settings.HotKeyReadSelected;
        
        MinimizeToTrayCheckBox.IsChecked = _settings.MinimizeToTray;
        StartMinimizedCheckBox.IsChecked = _settings.StartMinimized;
    }

    private void TestVoice_Click(object sender, RoutedEventArgs e)
    {
        if (VoiceComboBox.SelectedItem is string voiceName)
        {
            _ttsService.SetVoice(voiceName);
            _ttsService.Volume = (int)VolumeSlider.Value;
            _ttsService.SpeakAsyncNonBlocking("Привет! Это тестовое сообщение для проверки голоса.", SpeedSlider.Value);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.SelectedVoice = VoiceComboBox.SelectedItem as string ?? string.Empty;
        _settings.Volume = (int)VolumeSlider.Value;
        _settings.DefaultReadingSpeed = SpeedSlider.Value;
        _settings.HotKeyStartMonitoring = HotkeyStartTextBox.Text;
        _settings.HotKeyStopMonitoring = HotkeyStopTextBox.Text;
        _settings.HotKeyReadSelected = HotkeyReadTextBox.Text;
        _settings.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked ?? true;
        _settings.StartMinimized = StartMinimizedCheckBox.IsChecked ?? false;
        
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _ttsService.Stop();
        DialogResult = false;
        Close();
    }
}
