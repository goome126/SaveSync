using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace SaveSync.Models;

/// <summary>
/// Represents a game entry in the library with its save location.
/// </summary>
public partial class Game : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string SavePath { get; set; } = string.Empty;
    public DateTime LastSynced { get; set; }
    /// <summary>IGDB game id, populated when the game was found via IGDB search.</summary>
    public int? IgdbId { get; set; }
    /// <summary>IGDB cover image_id string, used to load/cache box art.</summary>
    public string? IgdbCoverImageId { get; set; }

    [ObservableProperty]
    private DateTime _lastSynced;
}
