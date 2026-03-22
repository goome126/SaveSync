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

    [ObservableProperty]
    private DateTime _lastSynced;
}
