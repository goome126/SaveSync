using System;
using System.IO;
using System.Text.Json;
using SaveSync.Models;

namespace SaveSync.Services;

/// <summary>
/// Manages application configuration persistence using JSON.
/// </summary>
public class ConfigurationService
{
    private readonly string _configDirectory;
    private readonly string _configFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ConfigurationService()
    {
        _configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SaveSync");
        
        _configFilePath = Path.Combine(_configDirectory, "config.json");
        
        EnsureConfigDirectoryExists();
    }

    private void EnsureConfigDirectoryExists()
    {
        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
        }
    }

    public AppConfig LoadConfig()
    {
        if (!File.Exists(_configFilePath))
        {
            return new AppConfig();
        }

        try
        {
            var json = File.ReadAllText(_configFilePath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void SaveConfig(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(_configFilePath, json);
    }

    public string GetConfigPath() => _configFilePath;
}
