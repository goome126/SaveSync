using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SaveSync.Services;

/// <summary>
/// A single game result returned from an IGDB search.
/// </summary>
public partial class IgdbSearchResult : ObservableObject
{
    public int IgdbId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CoverImageId { get; set; }
    public int? ReleaseYear { get; set; }

    [ObservableProperty]
    private Bitmap? _cover;
}

/// <summary>
/// Wraps the IGDB v4 API (authenticated via Twitch OAuth client-credentials).
/// Credentials can be supplied directly or via the IGDB_CLIENT_ID /
/// IGDB_CLIENT_SECRET environment variables.
/// Register a Twitch application at https://dev.twitch.tv/console to obtain them.
/// </summary>
public class IgdbService
{
    // Shared across all instances – one HttpClient per process is recommended.
    private static readonly HttpClient Http = new();

    private readonly string _clientId;
    private readonly string _clientSecret;

    // When set, this token is used directly and the client-credentials flow is skipped.
    private readonly string? _staticBearerToken;

    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SaveSync", "image-cache");

    static IgdbService()
    {
        Directory.CreateDirectory(CacheDir);
    }

    public IgdbService(string? clientId, string? clientSecret, string? bearerToken = null)
    {
        // A static bearer token takes priority — no secret needed.
        _staticBearerToken = !string.IsNullOrWhiteSpace(bearerToken) ? bearerToken
            : Environment.GetEnvironmentVariable("IGDB_BEARER_TOKEN");

        // Fall back to environment variables if the config values are empty.
        _clientId = (!string.IsNullOrWhiteSpace(clientId) ? clientId
            : Environment.GetEnvironmentVariable("IGDB_CLIENT_ID")) ?? string.Empty;

        _clientSecret = (!string.IsNullOrWhiteSpace(clientSecret) ? clientSecret
            : Environment.GetEnvironmentVariable("IGDB_CLIENT_SECRET")) ?? string.Empty;
    }

    /// <summary>True when valid credentials are available.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_clientId) &&
        (!string.IsNullOrWhiteSpace(_staticBearerToken) ||
         !string.IsNullOrWhiteSpace(_clientSecret));

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Search IGDB for games matching <paramref name="query"/>.</summary>
    public async Task<List<IgdbSearchResult>> SearchGamesAsync(string query, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(query))
            return new List<IgdbSearchResult>();

        await EnsureTokenAsync(ct);

        // IGDB uses an Apikalypse (FIQL-like) body syntax.
        var safeQuery = query.Replace("\"", "\\\"");
        var body = $"search \"{safeQuery}\"; fields name,cover.image_id,first_release_date; limit 8;";

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.com/v4/games");
        request.Headers.Add("Client-ID", _clientId);
        request.Headers.Add("Authorization", $"Bearer {_accessToken}");
        request.Content = new StringContent(body);

        using var response = await Http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseSearchResults(json);
    }

    /// <summary>
    /// Downloads (or loads from the disk cache) the cover art for
    /// <paramref name="result"/> and assigns <see cref="IgdbSearchResult.Cover"/>.
    /// </summary>
    public async Task LoadCoverAsync(IgdbSearchResult result, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(result.CoverImageId))
            return;

        try
        {
            result.Cover = await GetCoverBitmapAsync(result.CoverImageId, ct);
        }
        catch
        {
            // Cover load failure is non-fatal – leave Cover null.
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task EnsureTokenAsync(CancellationToken ct)
    {
        // Static bearer token: use it as-is, never refresh automatically.
        if (!string.IsNullOrWhiteSpace(_staticBearerToken))
        {
            _accessToken = _staticBearerToken;
            return;
        }

        if (_accessToken != null && DateTime.UtcNow < _tokenExpiry)
            return;

        var url = $"https://id.twitch.tv/oauth2/token" +
                  $"?client_id={Uri.EscapeDataString(_clientId)}" +
                  $"&client_secret={Uri.EscapeDataString(_clientSecret)}" +
                  $"&grant_type=client_credentials";

        using var response = await Http.PostAsync(url, content: null, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        _accessToken = root.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Twitch token response missing access_token");

        var expiresIn = root.GetProperty("expires_in").GetInt32();
        _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60);
    }

    private static List<IgdbSearchResult> ParseSearchResults(string json)
    {
        var results = new List<IgdbSearchResult>();

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return results;

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var igdbId = element.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
            var name = element.TryGetProperty("name", out var nameProp)
                ? (nameProp.GetString() ?? string.Empty) : string.Empty;

            string? imageId = null;
            if (element.TryGetProperty("cover", out var coverProp) &&
                coverProp.ValueKind == JsonValueKind.Object &&
                coverProp.TryGetProperty("image_id", out var imgProp))
            {
                imageId = imgProp.GetString();
            }

            int? releaseYear = null;
            if (element.TryGetProperty("first_release_date", out var dateProp))
            {
                // IGDB stores Unix timestamps.
                var epoch = DateTimeOffset.FromUnixTimeSeconds(dateProp.GetInt64());
                releaseYear = epoch.Year;
            }

            results.Add(new IgdbSearchResult
            {
                IgdbId = igdbId,
                Name = name,
                CoverImageId = imageId,
                ReleaseYear = releaseYear
            });
        }

        return results;
    }

    /// <summary>
    /// Reads cover art from the on-disk cache without making any network request.
    /// Returns null if the image hasn't been downloaded yet.
    /// </summary>
    public static Bitmap? TryLoadFromCache(string imageId)
    {
        var cachePath = Path.Combine(CacheDir, $"{imageId}.jpg");
        return File.Exists(cachePath) ? new Bitmap(cachePath) : null;
    }

    private static async Task<Bitmap?> GetCoverBitmapAsync(string imageId, CancellationToken ct)
    {
        var cachePath = Path.Combine(CacheDir, $"{imageId}.jpg");

        if (File.Exists(cachePath))
            return new Bitmap(cachePath);

        var url = $"https://images.igdb.com/igdb/image/upload/t_cover_small/{imageId}.jpg";
        var bytes = await Http.GetByteArrayAsync(url, ct);

        // Write to cache then load from the stream so we only parse once.
        await File.WriteAllBytesAsync(cachePath, bytes, ct);
        return new Bitmap(new MemoryStream(bytes));
    }
}
