using System;
using System.Threading.Tasks;

namespace SaveSync.Services;

/// <summary>
/// Interface for cloud storage providers (Google Drive, Dropbox, OneDrive, etc.)
/// </summary>
public interface ICloudStorageProvider
{
    /// <summary>
    /// Gets the name of the provider.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets whether the provider is currently authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Authenticates the user with the cloud provider.
    /// </summary>
    Task<bool> AuthenticateAsync();

    /// <summary>
    /// Syncs a local directory to cloud storage, mirroring local state (upload new/changed files, delete removed files).
    /// </summary>
    /// <param name="localPath">Local directory path to upload</param>
    /// <param name="destinationPath">Destination path in cloud storage</param>
    /// <param name="progress">Progress reporting callback</param>
    Task UploadDirectoryAsync(string localPath, string destinationPath, IProgress<int>? progress = null);

    /// <summary>
    /// Downloads a cloud directory to local storage, restoring all files.
    /// </summary>
    /// <param name="cloudPath">Path in cloud storage to download from</param>
    /// <param name="localPath">Local directory path to restore files into</param>
    /// <param name="progress">Progress reporting callback</param>
    Task DownloadDirectoryAsync(string cloudPath, string localPath, IProgress<int>? progress = null);

    /// <summary>
    /// Disconnects from the cloud provider.
    /// </summary>
    Task DisconnectAsync();
}
