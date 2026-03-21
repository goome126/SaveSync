using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SaveSync.ViewModels;

namespace SaveSync.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void BrowseSavePathButton_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as MainWindowViewModel;
        if (viewModel == null) return;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Game Save Directory",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            viewModel.NewGameSavePath = folders[0].Path.LocalPath;
        }
    }
}
