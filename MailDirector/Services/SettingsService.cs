using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MailDirector.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MailDirector.Services;

public class SettingsService : ISettingsService
{
    private readonly object _lock = new();
    private readonly object _debounceLock = new();
    private CancellationTokenSource? _debounceCts;
    private readonly IStorageService _storageService;
    private readonly string _configJsonPath;
    private readonly string _emailPreferencesPath;
    private readonly string _flightPlanYamlPath;
    private readonly string _flightPlanEmailPreferencesPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AppConfig Config { get; private set; } = new();

    public event EventHandler? ConfigChanged;

    public SettingsService(IStorageService storageService)
    {
        _storageService = storageService;
        _configJsonPath = _storageService.GetConfigPath();
        _emailPreferencesPath = _storageService.GetEmailPreferencesPath();

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _flightPlanYamlPath = Path.Combine(appData, "FlightPlan", "config.yaml");
        _flightPlanEmailPreferencesPath = Path.Combine(appData, "FlightPlan", "email_preferences.json");

        Load();
    }

    public void Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_configJsonPath))
                {
                    var json = File.ReadAllText(_configJsonPath);
                    var loaded = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                    if (loaded != null)
                    {
                        Config = loaded;
                        LoadFolderPreferencesIntoConfig();
                        return;
                    }
                }

                // If config.json doesn't exist, try importing from FlightPlan config.yaml
                if (File.Exists(_flightPlanYamlPath))
                {
                    try
                    {
                        var yaml = File.ReadAllText(_flightPlanYamlPath);
                        var deserializer = new DeserializerBuilder()
                            .WithNamingConvention(PascalCaseNamingConvention.Instance)
                            .IgnoreUnmatchedProperties()
                            .Build();

                        var loadedYaml = deserializer.Deserialize<AppConfig>(yaml);
                        if (loadedYaml != null)
                        {
                            Config = loadedYaml;
                            LoadFolderPreferencesIntoConfig();
                            Save(false);
                            return;
                        }
                    }
                    catch { }
                }

                // Also check local config.yaml
                if (File.Exists("config.yaml"))
                {
                    try
                    {
                        var yaml = File.ReadAllText("config.yaml");
                        var deserializer = new DeserializerBuilder()
                            .WithNamingConvention(PascalCaseNamingConvention.Instance)
                            .IgnoreUnmatchedProperties()
                            .Build();

                        var loadedYaml = deserializer.Deserialize<AppConfig>(yaml);
                        if (loadedYaml != null)
                        {
                            Config = loadedYaml;
                            LoadFolderPreferencesIntoConfig();
                            Save(false);
                            return;
                        }
                    }
                    catch { }
                }

                Config = new AppConfig();
                LoadFolderPreferencesIntoConfig();
            }
            catch
            {
                Config = new AppConfig();
            }
        }
    }

    private void LoadFolderPreferencesIntoConfig()
    {
        var prefs = LoadFolderPreferences();
        if (prefs != null && prefs.Count > 0)
        {
            Config.EmailPreferences.Folders = prefs;
        }
    }

    public Dictionary<string, FolderPreference> LoadFolderPreferences()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_emailPreferencesPath))
                {
                    var json = File.ReadAllText(_emailPreferencesPath);
                    var prefs = JsonSerializer.Deserialize<Dictionary<string, FolderPreference>>(json, JsonOptions);
                    if (prefs != null) return prefs;
                }

                // Try migrating from FlightPlan
                if (File.Exists(_flightPlanEmailPreferencesPath))
                {
                    var json = File.ReadAllText(_flightPlanEmailPreferencesPath);
                    var prefs = JsonSerializer.Deserialize<Dictionary<string, FolderPreference>>(json, JsonOptions);
                    if (prefs != null)
                    {
                        SaveFolderPreferences(prefs);
                        return prefs;
                    }
                }
            }
            catch { }

            return new Dictionary<string, FolderPreference>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void SaveFolderPreferences(Dictionary<string, FolderPreference> preferences)
    {
        lock (_lock)
        {
            try
            {
                Config.EmailPreferences.Folders = preferences;
                var json = JsonSerializer.Serialize(preferences, JsonOptions);
                var dir = Path.GetDirectoryName(_emailPreferencesPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(_emailPreferencesPath, json);
            }
            catch { }
        }
    }

    public void Save(bool raiseConfigChanged = true)
    {
        lock (_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_configJsonPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(Config, JsonOptions);
                File.WriteAllText(_configJsonPath, json);

                if (Config.EmailPreferences.Folders != null)
                {
                    SaveFolderPreferences(Config.EmailPreferences.Folders);
                }
            }
            catch { }
        }

        if (raiseConfigChanged)
        {
            ConfigChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SaveDebounced(int delayMs = 400, bool raiseConfigChanged = false)
    {
        lock (_debounceLock)
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            Task.Delay(delayMs, token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    Save(raiseConfigChanged);
                }
            }, TaskScheduler.Default);
        }
    }
}
