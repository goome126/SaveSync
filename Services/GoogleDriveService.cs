using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using SaveSync.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace SaveSync.Services;

/// <summary>
/// Google Drive implementation of cloud storage provider.
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

    private sealed record CloudFileInfo(string Id, string Name, bool IsFolder, string? Md5Checksum);

    public GoogleDriveService(ConfigurationService configService)
    {
        _configService = configService;
    }

    public async Task<bool> TryRestoreSessionAsync()
    {
        var credPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SaveSync", "token.json");

        if (!File.Exists("credentials.json") || !Directory.Exists(credPath))
            return false;

        try
        {
            using var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read);
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
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Session restore failed: {ex.Message}");
            _isAuthenticated = false;
            return false;
        }
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

    /// <summary>
    /// Syncs the local directory to Google Drive, mirroring local state:
    /// uploads new/changed files and deletes cloud files removed locally.
    /// </summary>
    public async Task UploadDirectoryAsync(string localPath, string destinationPath, IProgress<int>? progress = null)
    {
        if (!_isAuthenticated || _driveService == null)
            throw new InvalidOperationException("Not authenticated with Google Drive");

        if (!Directory.Exists(localPath))
            throw new DirectoryNotFoundException($"Local path not found: {localPath}");

        var folderId = await FindOrCreateFolderAsync(destinationPath);
        var cloudFiles = await ListFilesRecursiveAsync(folderId);
        var localFiles = Directory.GetFiles(localPath, "*", SearchOption.AllDirectories);

        int total = localFiles.Length;
        int done = 0;
        var processedCloudPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var localFile in localFiles)
        {
            var relativePath = Path.GetRelativePath(localPath, localFile).Replace('\\', '/');
            processedCloudPaths.Add(relativePath);

            if (cloudFiles.TryGetValue(relativePath, out var cloudFile) && !cloudFile.IsFolder)
            {
                var localMd5 = await ComputeMd5Async(localFile);
                if (!string.Equals(localMd5, cloudFile.Md5Checksum, StringComparison.OrdinalIgnoreCase))
                    await UpdateFileAsync(cloudFile.Id, localFile);
            }
            else
            {
                await UploadFileWithHierarchyAsync(localFile, folderId, relativePath, cloudFiles);
            }

            done++;
            progress?.Report(total > 0 ? done * 100 / total : 100);
        }

        // Delete cloud files that no longer exist locally
        foreach (var (relativePath, info) in cloudFiles)
        {
            if (!info.IsFolder && !processedCloudPaths.Contains(relativePath))
                await _driveService.Files.Delete(info.Id).ExecuteAsync();
        }
    }

    /// <summary>
    /// Downloads all files from the cloud path to local storage, recreating the directory structure.
    /// </summary>
    public async Task DownloadDirectoryAsync(string cloudPath, string localPath, IProgress<int>? progress = null)
    {
        if (!_isAuthenticated || _driveService == null)
            throw new InvalidOperationException("Not authenticated with Google Drive");

        var folderId = await FindFolderByPathAsync(cloudPath);
        if (folderId == null)
            throw new DirectoryNotFoundException($"No cloud backup found at: {cloudPath}");

        var cloudFiles = await ListFilesRecursiveAsync(folderId);
        var filesToDownload = cloudFiles.Where(f => !f.Value.IsFolder).ToList();

        int total = filesToDownload.Count;
        int done = 0;

        Directory.CreateDirectory(localPath);

        foreach (var (relativePath, info) in filesToDownload)
        {
            var localFilePath = Path.Combine(localPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(localFilePath)!);
            await DownloadFileAsync(info.Id, localFilePath);

            done++;
            progress?.Report(total > 0 ? done * 100 / total : 100);
        }
    }

    /// <summary>
    /// Returns true if the cloud folder at <paramref name="cloudPath"/> exists and contains any files.
    /// Uses a single API call with PageSize=1 so it returns as fast as possible.
    /// </summary>
    public async Task<bool> HasCloudFilesAsync(string cloudPath)
    {
        if (!_isAuthenticated || _driveService == null)
            return false;

        var folderId = await FindFolderByPathAsync(cloudPath);
        if (folderId == null)
            return false;

        var request = _driveService.Files.List();
        request.Q = $"'{folderId}' in parents and trashed=false";
        request.Fields = "files(id)";
        request.PageSize = 1;
        var result = await request.ExecuteAsync();
        return result.Files.Count > 0;
    }

    private async Task<Dictionary<string, CloudFileInfo>> ListFilesRecursiveAsync(string folderId, string relativePrefix = "")
    {
        var result = new Dictionary<string, CloudFileInfo>(StringComparer.OrdinalIgnoreCase);
        string? pageToken = null;

        do
        {
            var request = _driveService!.Files.List();
            request.Q = $"'{folderId}' in parents and trashed=false";
            request.Fields = "nextPageToken, files(id, name, mimeType, md5Checksum)";
            request.PageSize = 1000;
            if (pageToken != null) request.PageToken = pageToken;

            var response = await request.ExecuteAsync();
            pageToken = response.NextPageToken;

            foreach (var file in response.Files)
            {
                var isFolder = file.MimeType == "application/vnd.google-apps.folder";
                var relativePath = string.IsNullOrEmpty(relativePrefix)
                    ? file.Name
                    : $"{relativePrefix}/{file.Name}";

                result[relativePath] = new CloudFileInfo(file.Id, file.Name, isFolder, file.Md5Checksum);

                if (isFolder)
                {
                    var subFiles = await ListFilesRecursiveAsync(file.Id, relativePath);
                    foreach (var kvp in subFiles)
                        result[kvp.Key] = kvp.Value;
                }
            }
        } while (pageToken != null);

        return result;
    }

    private async Task<string> FindOrCreateFolderAsync(string folderPath)
    {
        var parts = folderPath.Split('/', '\\');
        string? parentId = null;

        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part)) continue;

            var folder = await FindFolderAsync(part, parentId);
            if (folder == null)
            {
                var fileMetadata = new Google.Apis.Drive.v3.Data.File
                {
                    Name = part,
                    MimeType = "application/vnd.google-apps.folder",
                    Parents = parentId != null ? new[] { parentId } : null
                };

                var request = _driveService!.Files.Create(fileMetadata);
                request.Fields = "id";
                var created = await request.ExecuteAsync();
                parentId = created.Id;
            }
            else
            {
                parentId = folder.Id;
            }
        }

        return parentId!;
    }

    private async Task<string?> FindFolderByPathAsync(string folderPath)
    {
        var parts = folderPath.Split('/', '\\');
        string? parentId = null;

        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            var folder = await FindFolderAsync(part, parentId);
            if (folder == null) return null;
            parentId = folder.Id;
        }

        return parentId;
    }

    private async Task<string> FindOrCreateSubfolderAsync(string rootFolderId, string relativeDir, Dictionary<string, CloudFileInfo> cloudFiles)
    {
        var parts = relativeDir.Split('/');
        string currentFolderId = rootFolderId;
        string currentPath = "";

        foreach (var part in parts)
        {
            var folderPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";

            if (cloudFiles.TryGetValue(folderPath, out var existing) && existing.IsFolder)
            {
                currentFolderId = existing.Id;
            }
            else
            {
                var fileMetadata = new Google.Apis.Drive.v3.Data.File
                {
                    Name = part,
                    MimeType = "application/vnd.google-apps.folder",
                    Parents = new[] { currentFolderId }
                };
                var req = _driveService!.Files.Create(fileMetadata);
                req.Fields = "id";
                var created = await req.ExecuteAsync();
                currentFolderId = created.Id;
                cloudFiles[folderPath] = new CloudFileInfo(created.Id, part, true, null);
            }

            currentPath = folderPath;
        }

        return currentFolderId;
    }

    private async Task<Google.Apis.Drive.v3.Data.File?> FindFolderAsync(string name, string? parentId)
    {
        var query = $"name='{name}' and mimeType='application/vnd.google-apps.folder' and trashed=false";
        if (parentId != null)
            query += $" and '{parentId}' in parents";

        var request = _driveService!.Files.List();
        request.Q = query;
        request.Fields = "files(id, name)";

        var result = await request.ExecuteAsync();
        return result.Files.FirstOrDefault();
    }

    private async Task UploadFileWithHierarchyAsync(string localFilePath, string rootFolderId, string relativeCloudPath, Dictionary<string, CloudFileInfo> cloudFiles)
    {
        var parts = relativeCloudPath.Split('/');
        var fileName = parts[^1];
        var targetFolderId = rootFolderId;

        if (parts.Length > 1)
        {
            var relativeDir = string.Join('/', parts[..^1]);
            targetFolderId = await FindOrCreateSubfolderAsync(rootFolderId, relativeDir, cloudFiles);
        }

        var fileMetadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = fileName,
            Parents = new[] { targetFolderId }
        };

        using var stream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read);
        var request = _driveService!.Files.Create(fileMetadata, stream, "application/octet-stream");
        request.Fields = "id";
        var uploadProgress = await request.UploadAsync();
        if (uploadProgress.Status != Google.Apis.Upload.UploadStatus.Completed)
            throw new IOException($"Upload failed for '{fileName}': {uploadProgress.Exception?.Message}");
    }

    private async Task UpdateFileAsync(string fileId, string localFilePath)
    {
        using var stream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read);
        var fileMetadata = new Google.Apis.Drive.v3.Data.File();
        var request = _driveService!.Files.Update(fileMetadata, fileId, stream, "application/octet-stream");
        request.Fields = "id";
        var uploadProgress = await request.UploadAsync();
        if (uploadProgress.Status != Google.Apis.Upload.UploadStatus.Completed)
            throw new IOException($"Update failed for '{Path.GetFileName(localFilePath)}': {uploadProgress.Exception?.Message}");
    }

    private async Task DownloadFileAsync(string fileId, string localFilePath)
    {
        // Write to a temp file first so an interrupted download never overwrites a valid save
        var tempPath = localFilePath + ".savesync_tmp";
        try
        {
            var request = _driveService!.Files.Get(fileId);
            Google.Apis.Download.IDownloadProgress downloadProgress;
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                downloadProgress = await request.DownloadAsync(stream);
            }

            if (downloadProgress.Status != Google.Apis.Download.DownloadStatus.Completed)
                throw new IOException($"Download failed for file ID '{fileId}': {downloadProgress.Exception?.Message}");

            File.Move(tempPath, localFilePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }

    private static async Task<string> ComputeMd5Async(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await Task.Run(() => md5.ComputeHash(stream));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task DisconnectAsync()
    {
        if (_credential != null)
            await _credential.RevokeTokenAsync(CancellationToken.None);

        _driveService?.Dispose();
        _driveService = null;
        _credential = null;
        _isAuthenticated = false;

        var config = _configService.LoadConfig();
        config.GoogleDriveSettings = null;
        _configService.SaveConfig(config);

        var credPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SaveSync", "token.json");

        if (Directory.Exists(credPath))
            Directory.Delete(credPath, true);
    }
}
