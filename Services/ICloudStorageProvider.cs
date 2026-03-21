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
    /// Uploads a directory to the cloud storage.
    /// </summary>
    /// <param name="localPath">Local directory path to upload</param>
    /// <param name="destinationPath">Destination path in cloud storage</param>
    /// <param name="progress">Progress reporting callback</param>
    Task UploadDirectoryAsync(string localPath, string destinationPath, IProgress<int>? progress = null);

    /// <summary>
    /// Disconnects from the cloud provider.
    /// </summary>
    Task DisconnectAsync();
}
