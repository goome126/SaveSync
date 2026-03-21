using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SaveSync.Views;

public partial class AddGameDialog : Window
{
    public AddGameDialog()
    {
        InitializeComponent();
    }

    private async void BrowseSavePathButton_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Game Save Directory",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            SavePathBox.Text = folders[0].Path.LocalPath;
        }
    }

    private void AddGame_Click(object? sender, RoutedEventArgs e)
    {
        var name = GameNameBox.Text?.Trim();
        var path = SavePathBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
        {
            ErrorText.Text = "Please enter both a game name and a save directory path.";
            ErrorText.IsVisible = true;
            return;
        }

        Close((name, path));
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
