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
    public IgdbSettings IgdbSettings { get; set; } = new();
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

/// <summary>
/// IGDB API credentials. Obtain a Twitch application client ID and secret at
/// https://dev.twitch.tv/console and store them here (or set environment
/// variables IGDB_CLIENT_ID / IGDB_CLIENT_SECRET).
///
/// Config file location:
///   macOS:   ~/Library/Application Support/SaveSync/config.json
///   Windows: %AppData%\SaveSync\config.json
///   Linux:   ~/.config/SaveSync/config.json
///
/// Public-app alternative: if your Twitch app has no client secret, set
/// ClientId and obtain a bearer token via another means (e.g. the Twitch CLI:
/// `twitch token` or the implicit grant flow) then store it in BearerToken
/// (env var: IGDB_BEARER_TOKEN). The client credentials flow is skipped and
/// the token is used directly; it is not refreshed automatically.
/// </summary>
public class IgdbSettings
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    /// <summary>Optional pre-obtained Twitch app access token. Takes priority over ClientId/Secret.</summary>
    public string? BearerToken { get; set; }
}
