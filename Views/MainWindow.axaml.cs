using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SaveSync.Services;
using SaveSync.ViewModels;
using System.Threading.Tasks;

namespace SaveSync.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
                vm.SetDialogService(new WindowDialogService(this));
        };
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

    private sealed class WindowDialogService : IDialogService
    {
        private readonly Window _owner;

        public WindowDialogService(Window owner) => _owner = owner;

        public async Task<bool> ConfirmAsync(string title, string message)
        {
            var dialog = new ConfirmationDialog(title, message);
            return await dialog.ShowDialog<bool>(_owner);
        }
    }
}

