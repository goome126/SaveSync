using System;

namespace SaveSync.Models;

/// <summary>
/// Represents a game entry in the library with its save location.
/// </summary>
public class Game
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string SavePath { get; set; } = string.Empty;
    public DateTime LastSynced { get; set; }
}
