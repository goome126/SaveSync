using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveSync.Models;
using SaveSync.Services;

namespace SaveSync.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ConfigurationService _configService;
    private readonly GoogleDriveService _googleDriveService;
    private readonly SyncService _syncService;
    private IDialogService? _dialogService;

    public void SetDialogService(IDialogService dialogService) => _dialogService = dialogService;

    [ObservableProperty]
    private ObservableCollection<Game> _games = new();

    [ObservableProperty]
    private Game? _selectedGame;

    [ObservableProperty]
    private string _newGameName = string.Empty;

    [ObservableProperty]
    private string _newGameSavePath = string.Empty;

    [ObservableProperty]
    private string _cloudDestinationFolder = "SaveSync/Saves";

    [ObservableProperty]
    private bool _isGoogleDriveConnected;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SyncSelectedGameCommand))]
    [NotifyCanExecuteChangedFor(nameof(SyncAllGamesCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreSelectedGameCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreAllGamesCommand))]
    [NotifyPropertyChangedFor(nameof(IsWorking))]
    private bool _isSyncing;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SyncSelectedGameCommand))]
    [NotifyCanExecuteChangedFor(nameof(SyncAllGamesCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreSelectedGameCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreAllGamesCommand))]
    [NotifyPropertyChangedFor(nameof(IsWorking))]
    private bool _isRestoring;

    public bool IsWorking => IsSyncing || IsRestoring;

    [ObservableProperty]
    private int _syncProgress;

    public MainWindowViewModel()
    {
        _configService = new ConfigurationService();
        _googleDriveService = new GoogleDriveService(_configService);
        _syncService = new SyncService(_googleDriveService, _configService);

        LoadConfiguration();
        _ = RestoreGoogleDriveSessionAsync();
    }

    private async Task RestoreGoogleDriveSessionAsync()
    {
        StatusMessage = "Restoring Google Drive session...";
        var restored = await _googleDriveService.TryRestoreSessionAsync();
        IsGoogleDriveConnected = restored;
        StatusMessage = restored ? "Google Drive session restored" : "Ready";
    }

    private void LoadConfiguration()
    {
        var config = _configService.LoadConfig();

        Games.Clear();
        foreach (var game in config.Games)
        {
            Games.Add(game);
        }

        CloudDestinationFolder = config.CloudDestinationFolder;
    }

    [RelayCommand]
    private void AddGame()
    {
        if (string.IsNullOrWhiteSpace(NewGameName) || string.IsNullOrWhiteSpace(NewGameSavePath))
        {
            StatusMessage = "Please enter both game name and save path";
            return;
        }

        var game = new Game
        {
            Name = NewGameName.Trim(),
            SavePath = NewGameSavePath.Trim()
        };

        Games.Add(game);

        var config = _configService.LoadConfig();
        config.Games.Add(game);
        _configService.SaveConfig(config);

        NewGameName = string.Empty;
        NewGameSavePath = string.Empty;
        StatusMessage = $"Added {game.Name} to library";
    }

    [RelayCommand]
    private void RemoveGame()
    {
        var gameToRemove = SelectedGame;
        if (gameToRemove == null)
        {
            StatusMessage = "Please select a game to remove";
            return;
        }

        var gameName = gameToRemove.Name;
        Games.Remove(gameToRemove);

        var config = _configService.LoadConfig();
        var removedCount = config.Games.RemoveAll(g =>
            (!string.IsNullOrWhiteSpace(gameToRemove.Id) && g.Id == gameToRemove.Id) ||
            (string.IsNullOrWhiteSpace(gameToRemove.Id) && g.Name == gameToRemove.Name && g.SavePath == gameToRemove.SavePath));

        _configService.SaveConfig(config);

        SelectedGame = null;
        StatusMessage = removedCount > 0
            ? $"Removed {gameName} from library"
            : $"Removed {gameName} from list, but no matching config entry was found";
    }

    [RelayCommand]
    private async Task ConnectGoogleDrive()
    {
        StatusMessage = "Connecting to Google Drive...";

        try
        {
            var success = await _googleDriveService.AuthenticateAsync();
            IsGoogleDriveConnected = success;
            StatusMessage = success 
                ? "Connected to Google Drive successfully" 
                : "Failed to connect to Google Drive";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error connecting to Google Drive: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DisconnectGoogleDrive()
    {
        await _googleDriveService.DisconnectAsync();
        IsGoogleDriveConnected = false;
        StatusMessage = "Disconnected from Google Drive";
    }

    [RelayCommand]
    private void UpdateCloudFolder()
    {
        var config = _configService.LoadConfig();
        config.CloudDestinationFolder = CloudDestinationFolder;
        _configService.SaveConfig(config);
        StatusMessage = $"Cloud destination set to: {CloudDestinationFolder}";
    }

    [RelayCommand(CanExecute = nameof(CanRunSyncCommands))]
    private async Task SyncSelectedGame()
    {
        if (SelectedGame == null) return;

        if (!IsGoogleDriveConnected)
        {
            StatusMessage = "Please connect to Google Drive first";
            return;
        }

        StatusMessage = "Checking save directory...";
        if (!await ConfirmSyncIfLocalEmptyAsync(SelectedGame))
        {
            StatusMessage = $"Sync cancelled for {SelectedGame.Name}";
            return;
        }

        IsSyncing = true;
        SyncProgress = 0;
        StatusMessage = $"Syncing {SelectedGame.Name}...";

        try
        {
            var progress = new Progress<int>(p => SyncProgress = p);
            await _syncService.SyncGameAsync(SelectedGame, progress);

            StatusMessage = $"Successfully synced {SelectedGame.Name}";
            SyncProgress = 100;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error syncing {SelectedGame.Name}: {ex.Message}";
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunSyncCommands))]
    private async Task SyncAllGames()
    {
        if (!IsGoogleDriveConnected)
        {
            StatusMessage = "Please connect to Google Drive first";
            return;
        }

        if (Games.Count == 0)
        {
            StatusMessage = "No games in library to sync";
            return;
        }

        IsSyncing = true;
        SyncProgress = 0;
        StatusMessage = "Syncing all games...";

        try
        {
            int total = Games.Count;
            int synced = 0;

            for (int i = 0; i < total; i++)
            {
                var game = Games[i];
                StatusMessage = $"Checking {game.Name} ({i + 1}/{total})...";

                if (!await ConfirmSyncIfLocalEmptyAsync(game))
                    continue;

                StatusMessage = $"Syncing {game.Name} ({i + 1}/{total})...";
                await _syncService.SyncGameAsync(game);
                synced++;
                SyncProgress = (i + 1) * 100 / total;
            }

            StatusMessage = synced == total
                ? $"Successfully synced all {total} games"
                : $"Synced {synced} of {total} games (some were skipped)";
            SyncProgress = 100;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error syncing games: {ex.Message}";
        }
        finally
        {
            IsSyncing = false;
        }
    }

    private bool CanRunSyncCommands() => !IsSyncing && !IsRestoring;

    /// <summary>
    /// If the local save directory is empty but a cloud backup exists, shows a confirmation
    /// dialog warning the user that syncing will delete the cloud backup.
    /// Returns false if the user cancels and the sync should be aborted.
    /// </summary>
    private async Task<bool> ConfirmSyncIfLocalEmptyAsync(Game game)
    {
        if (_dialogService == null)
            return true;

        bool localIsEmpty;
        try
        {
            localIsEmpty = !Directory.Exists(game.SavePath) ||
                           !Directory.EnumerateFiles(game.SavePath, "*", SearchOption.AllDirectories).Any();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not enumerate local save directory: {ex.Message}");
            return true;
        }

        if (!localIsEmpty)
            return true;

        var config = _configService.LoadConfig();
        var cloudPath = $"{config.CloudDestinationFolder}/{game.Name}";
        var hasCloudFiles = await _googleDriveService.HasCloudFilesAsync(cloudPath);

        if (!hasCloudFiles)
            return true;

        return await _dialogService.ConfirmAsync(
            "Empty Local Directory",
            $"The local save directory for \u2018{game.Name}\u2019 is empty, " +
            $"but a cloud backup exists.\n\n" +
            $"Proceeding will permanently delete the cloud backup for this game. " +
            $"Are you sure you want to continue?");
    }

    [RelayCommand(CanExecute = nameof(CanRunSyncCommands))]
    private async Task RestoreSelectedGame()
    {
        if (SelectedGame == null) return;

        if (!IsGoogleDriveConnected)
        {
            StatusMessage = "Please connect to Google Drive first";
            return;
        }

        IsRestoring = true;
        SyncProgress = 0;
        StatusMessage = $"Restoring {SelectedGame.Name}...";

        try
        {
            var progress = new Progress<int>(p => SyncProgress = p);
            await _syncService.RestoreGameAsync(SelectedGame, progress);

            StatusMessage = $"Successfully restored {SelectedGame.Name}";
            SyncProgress = 100;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error restoring {SelectedGame.Name}: {ex.Message}";
        }
        finally
        {
            IsRestoring = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunSyncCommands))]
    private async Task RestoreAllGames()
    {
        if (!IsGoogleDriveConnected)
        {
            StatusMessage = "Please connect to Google Drive first";
            return;
        }

        if (Games.Count == 0)
        {
            StatusMessage = "No games in library to restore";
            return;
        }

        IsRestoring = true;
        SyncProgress = 0;
        StatusMessage = "Restoring all games...";

        try
        {
            var progress = new Progress<(int current, int total, string gameName)>(p =>
            {
                SyncProgress = p.current * 100 / p.total;
                StatusMessage = $"Restoring {p.gameName} ({p.current}/{p.total})...";
            });

            await _syncService.RestoreAllGamesAsync(progress);

            StatusMessage = $"Successfully restored all {Games.Count} games";
            SyncProgress = 100;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error restoring games: {ex.Message}";
        }
        finally
        {
            IsRestoring = false;
        }
    }
}
