using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using SaveSync.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SaveSync.Services;

/// <summary>
/// Google Drive implementation of cloud storage provider.
/// Note: This is a placeholder implementation for MVP.
/// Full Google Drive API integration requires additional NuGet packages and OAuth setup.
/// </summary>
public class GoogleDriveService : ICloudStorageProvider
{
    private readonly ConfigurationService _configService;
    private bool _isAuthenticated;
    private DriveService? _driveService;
    private UserCredential? _credential;
    private readonly string[] _scopes = { DriveService.Scope.DriveFile };
    private const string ApplicationName = "SaveSync";

    public string ProviderName => "Google Drive";
    public bool IsAuthenticated => _isAuthenticated;

    public GoogleDriveService(ConfigurationService configService)
    {
        _configService = configService;
        var config = _configService.LoadConfig();
        _isAuthenticated = config.GoogleDriveSettings?.IsConnected ?? false;
    }

    public async Task<bool> AuthenticateAsync()
    {
        try
        {
            using var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read);

            var credPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SaveSync", "token.json");

            var clientSecrets = await GoogleClientSecrets.FromStreamAsync(stream, CancellationToken.None);
            _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                clientSecrets.Secrets,
                _scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(credPath, true));

            _driveService = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = _credential,
                ApplicationName = ApplicationName
            });

            _isAuthenticated = true;

            var config = _configService.LoadConfig();
            config.GoogleDriveSettings = new CloudProviderSettings
            {
                IsConnected = true,
                LastConnected = DateTime.Now,
                RefreshToken = _credential.Token.RefreshToken
            };
            _configService.SaveConfig(config);

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Authentication failed: {ex.Message}");
            return false;
        }
    }

    public async Task UploadDirectoryAsync(string localPath, string destinationPath, IProgress<int>? progress = null)
    {
        if (!_isAuthenticated || _driveService == null)
        {
            throw new InvalidOperationException("Not authenticated with Google Drive");
        }

        if (!Directory.Exists(localPath))
        {
            throw new DirectoryNotFoundException($"Local path not found: {localPath}");
        }

        // Find or create the destination folder
        var folderId = await FindOrCreateFolderAsync(destinationPath);

        // Upload all files
        var files = Directory.GetFiles(localPath, "*", SearchOption.AllDirectories);

        for (int i = 0; i < files.Length; i++)
        {
            var file = files[i];
            var relativePath = Path.GetRelativePath(localPath, file);

            await UploadFileAsync(file, folderId, relativePath);

            progress?.Report((i + 1) * 100 / files.Length);
        }
    }

    private async Task<string> FindOrCreateFolderAsync(string folderPath)
    {
        var parts = folderPath.Split('/', '\\');
        string? parentId = null;

        foreach (var part in parts)
        {
            var folder = await FindFolderAsync(part, parentId);

            if (folder == null)
            {
                // Create folder
                var fileMetadata = new Google.Apis.Drive.v3.Data.File
                {
                    Name = part,
                    MimeType = "application/vnd.google-apps.folder",
                    Parents = parentId != null ? new[] { parentId } : null
                };

                var request = _driveService!.Files.Create(fileMetadata);
                request.Fields = "id";
                var createdFolder = await request.ExecuteAsync();
                parentId = createdFolder.Id;
            }
            else
            {
                parentId = folder.Id;
            }
        }

        return parentId!;
    }

    private async Task<Google.Apis.Drive.v3.Data.File?> FindFolderAsync(string name, string? parentId)
    {
        var query = $"name='{name}' and mimeType='application/vnd.google-apps.folder' and trashed=false";

        if (parentId != null)
        {
            query += $" and '{parentId}' in parents";
        }

        var request = _driveService!.Files.List();
        request.Q = query;
        request.Fields = "files(id, name)";

        var result = await request.ExecuteAsync();
        return result.Files.FirstOrDefault();
    }

    private async Task UploadFileAsync(string filePath, string folderId, string fileName)
    {
        var fileMetadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = fileName,
            Parents = new[] { folderId }
        };

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

        var request = _driveService!.Files.Create(fileMetadata, stream, GetMimeType(filePath));
        request.Fields = "id";

        await request.UploadAsync();
    }

    private static string GetMimeType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
    }

    public async Task DisconnectAsync()
    {
        if (_credential != null)
        {
            await _credential.RevokeTokenAsync(CancellationToken.None);
        }

        _driveService?.Dispose();
        _driveService = null;
        _credential = null;
        _isAuthenticated = false;

        var config = _configService.LoadConfig();
        config.GoogleDriveSettings = null;
        _configService.SaveConfig(config);

        // Delete token file
        var credPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SaveSync", "token.json");

        if (Directory.Exists(credPath))
        {
            Directory.Delete(credPath, true);
        }
    }
}
