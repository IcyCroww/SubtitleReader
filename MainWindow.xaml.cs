using System.ComponentModel;
using System.Windows;
using SubtitleReader.Services;
using SubtitleReader.ViewModels;

namespace SubtitleReader;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly HotkeyService _hotkeyService;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        _hotkeyService = new HotkeyService();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Инициализируем горячие клавиши после загрузки окна
        _hotkeyService.Initialize(this);

        // F9 - Старт/Стоп мониторинга
        _hotkeyService.RegisterHotkey("F9", () =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_viewModel.IsMonitoringActive)
                    _viewModel.StopMonitoringCommand.Execute(null);
                else
                    _viewModel.StartMonitoringCommand.Execute(null);
            });
        });

        // F10 - Остановить чтение
        _hotkeyService.RegisterHotkey("F10", () =>
        {
            Dispatcher.Invoke(() => _viewModel.StopReadingCommand.Execute(null));
        });

        // Ctrl+F9 - Читать выбранную область
        _hotkeyService.RegisterHotkey("Ctrl+F9", () =>
        {
            Dispatcher.Invoke(() => _viewModel.ReadRegionCommand.Execute(null));
        });
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        _hotkeyService.Dispose();
        _viewModel.Dispose();
    }
}
