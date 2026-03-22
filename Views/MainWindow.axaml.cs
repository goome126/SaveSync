using Avalonia.Controls;
using Avalonia.Interactivity;
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

    private async void AddGameButton_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as MainWindowViewModel;
        if (viewModel == null) return;

        var dialog = new AddGameDialog();
        var result = await dialog.ShowDialog<(string Name, string Path, int? IgdbId, string? CoverImageId)?>(this);

        if (result is { } game)
        {
            viewModel.NewGameName = game.Name;
            viewModel.NewGameSavePath = game.Path;
            viewModel.NewGameIgdbId = game.IgdbId;
            viewModel.NewGameIgdbCoverImageId = game.CoverImageId;
            viewModel.AddGameCommand.Execute(null);
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

