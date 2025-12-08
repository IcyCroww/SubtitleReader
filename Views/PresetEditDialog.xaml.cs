using System.Windows;
using SubtitleReader.Models;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace SubtitleReader.Views;

public partial class PresetEditDialog : Window
{
    public string PresetName { get; private set; } = string.Empty;
    public string PresetDescription { get; private set; } = string.Empty;

    public PresetEditDialog()
    {
        InitializeComponent();
        NameTextBox.Focus();
    }

    public PresetEditDialog(GamePreset preset) : this()
    {
        NameTextBox.Text = preset.Name;
        DescriptionTextBox.Text = preset.Description;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            MessageBox.Show("Введите название заготовки", "Ошибка", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            NameTextBox.Focus();
            return;
        }

        PresetName = NameTextBox.Text.Trim();
        PresetDescription = DescriptionTextBox.Text?.Trim() ?? string.Empty;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
