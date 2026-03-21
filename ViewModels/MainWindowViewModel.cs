using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
    private bool _isSyncing;

    [ObservableProperty]
    private int _syncProgress;

    public MainWindowViewModel()
    {
        _configService = new ConfigurationService();
        _googleDriveService = new GoogleDriveService(_configService);
        _syncService = new SyncService(_googleDriveService, _configService);

        LoadConfiguration();
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
        IsGoogleDriveConnected = _googleDriveService.IsAuthenticated;
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
            var progress = new Progress<(int current, int total, string gameName)>(p =>
            {
                SyncProgress = p.current * 100 / p.total;
                StatusMessage = $"Syncing {p.gameName} ({p.current}/{p.total})...";
            });

            await _syncService.SyncAllGamesAsync(progress);

            StatusMessage = $"Successfully synced all {Games.Count} games";
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

    private bool CanRunSyncCommands() => !IsSyncing;
}
