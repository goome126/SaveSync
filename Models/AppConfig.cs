using System;
using System.Collections.Generic;

namespace SaveSync.Models;

/// <summary>
/// Application configuration including game library and cloud settings.
/// </summary>
public class AppConfig
{
    public List<Game> Games { get; set; } = new();
    public string CloudDestinationFolder { get; set; } = "SaveSync/Saves";
    public CloudProviderSettings? GoogleDriveSettings { get; set; }
}

/// <summary>
/// Cloud provider settings for authentication and configuration.
/// </summary>
public class CloudProviderSettings
{
    public bool IsConnected { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? LastConnected { get; set; }
}
