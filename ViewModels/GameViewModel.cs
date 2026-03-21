using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using SaveSync.Models;
using SaveSync.Services;

namespace SaveSync.ViewModels;

/// <summary>
/// Wraps a <see cref="Game"/> with UI-bindable properties, including an
/// observable <see cref="Cover"/> bitmap loaded from the IGDB image cache.
/// </summary>
public partial class GameViewModel : ObservableObject
{
    public Game Game { get; }

    [ObservableProperty]
    private Bitmap? _cover;

    // Forwarded read-only bindings so AXAML can bind directly without ".Game.*"
    public string Name => Game.Name;
    public string SavePath => Game.SavePath;
    public DateTime LastSynced => Game.LastSynced;
    public int? IgdbId => Game.IgdbId;
    public string? IgdbCoverImageId => Game.IgdbCoverImageId;

    public GameViewModel(Game game)
    {
        Game = game;
        TryLoadCoverFromCache();
    }

    /// <summary>
    /// Attempts to load the cover art from the local disk cache.
    /// No network request is made; if the image isn't cached it stays null.
    /// </summary>
    private void TryLoadCoverFromCache()
    {
        if (!string.IsNullOrEmpty(Game.IgdbCoverImageId))
            Cover = IgdbService.TryLoadFromCache(Game.IgdbCoverImageId);
    }
}
