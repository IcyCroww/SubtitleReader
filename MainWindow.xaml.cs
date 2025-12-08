using System.ComponentModel;
using System.Windows;
using SubtitleReader.ViewModels;

namespace SubtitleReader;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        _viewModel.Dispose();
    }
}
