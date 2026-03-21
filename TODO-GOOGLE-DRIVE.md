# TODO: Complete Google Drive Integration

This file outlines the steps needed to replace the placeholder Google Drive implementation with real API integration.

## Prerequisites

- [X] Google Account
- [X] Google Cloud Console access

## Step 1: Install Google Drive API Package

```bash
dotnet add package Google.Apis.Drive.v3
dotnet add package Google.Apis.Auth
```

## Step 2: Google Cloud Console Setup

1. [ ] Go to [Google Cloud Console](https://console.cloud.google.com/)
2. [ ] Create a new project or select existing one
3. [ ] Enable Google Drive API
   - Navigate to "APIs & Services" > "Library"
   - Search for "Google Drive API"
   - Click "Enable"
4. [ ] Create OAuth 2.0 Credentials
   - Go to "APIs & Services" > "Credentials"
   - Click "Create Credentials" > "OAuth client ID"
   - Choose "Desktop app" as application type
   - Download the credentials JSON file
   - Save as `credentials.json` in the project root

## Step 3: Update GoogleDriveService.cs

### Add Using Statements

```csharp
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
```

### Add Fields

```csharp
private DriveService? _driveService;
private UserCredential? _credential;
private readonly string[] _scopes = { DriveService.Scope.DriveFile };
private const string ApplicationName = "SaveSync";
```

### Implement Real Authentication

Replace the `AuthenticateAsync` method:

```csharp
public async Task<bool> AuthenticateAsync()
{
    try
    {
        using var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read);
        
        var credPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SaveSync", "token.json");

        _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.Load(stream).Secrets,
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
```

### Implement Real Upload

Replace the `UploadDirectoryAsync` method:

```csharp
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
```

### Implement Real Disconnect

```csharp
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
```

## Step 4: Add Credentials to .gitignore

Add to `.gitignore`:
```
credentials.json
token.json/
```

## Step 5: Test

1. [ ] Run the application
2. [ ] Click "Connect Google Drive"
3. [ ] Browser should open for OAuth consent
4. [ ] Grant permissions
5. [ ] Add a test game with a real save directory
6. [ ] Click "Sync Selected Game"
7. [ ] Verify files appear in Google Drive

## Additional Features to Implement

- [ ] Download/restore functionality
- [ ] Conflict resolution (what if cloud has newer files?)
- [ ] Incremental sync (only upload changed files)
- [ ] File comparison by hash
- [ ] Sync history tracking
- [ ] Error retry logic
- [ ] Rate limiting handling
- [ ] Large file resumable uploads
- [ ] Selective file sync (exclude certain file types)

## Optional: Add Other Providers

Follow the same pattern for:
- [ ] Dropbox
- [ ] OneDrive
- [ ] AWS S3
- [ ] Custom FTP/SFTP

Each would implement `ICloudStorageProvider` interface.

---

**Note**: The current placeholder implementation allows you to test the UI and flow without needing Google Drive API credentials. When ready to go live, follow this TODO to enable real cloud sync.
