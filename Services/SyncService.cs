using System;
using System.IO;
using System.Threading.Tasks;
using SaveSync.Models;

namespace SaveSync.Services;

/// <summary>
/// Handles synchronization operations for games.
/// </summary>
public class SyncService
{
    private readonly ICloudStorageProvider _cloudProvider;
    private readonly ConfigurationService _configService;

    public SyncService(ICloudStorageProvider cloudProvider, ConfigurationService configService)
    {
        _cloudProvider = cloudProvider;
        _configService = configService;
    }

    public async Task<bool> SyncGameAsync(Game game, IProgress<int>? progress = null)
    {
        if (!_cloudProvider.IsAuthenticated)
        {
            throw new InvalidOperationException("Cloud provider not authenticated");
        }

        var config = _configService.LoadConfig();
        var destinationPath = Path.Combine(config.CloudDestinationFolder, game.Name);

        await _cloudProvider.UploadDirectoryAsync(game.SavePath, destinationPath, progress);

        // Update last synced time
        game.LastSynced = DateTime.Now;
        _configService.SaveConfig(config);

        return true;
    }

    public async Task<bool> SyncAllGamesAsync(IProgress<(int current, int total, string gameName)>? progress = null)
    {
        var config = _configService.LoadConfig();
        var games = config.Games;

        for (int i = 0; i < games.Count; i++)
        {
            var game = games[i];
            progress?.Report((i + 1, games.Count, game.Name));
            
            await SyncGameAsync(game);
        }

        return true;
    }
}
