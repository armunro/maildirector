using System;
using System.IO;

namespace MailDirector.Services;

public interface IStorageService
{
    string GetBasePath();
    string GetConfigPath();
    string GetRulesDirectory();
    string GetEmailPreferencesPath();
    string GetAuthRecordPath();
}

public class StorageService : IStorageService
{
    private readonly string _basePath;

    public StorageService(string? customBasePath = null)
    {
        if (!string.IsNullOrWhiteSpace(customBasePath))
        {
            _basePath = customBasePath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var flightMailPath = Path.Combine(appData, "MailDirector");
            var flightPlanPath = Path.Combine(appData, "FlightPlan");

            // Prefer existing FlightPlan or MailDirector directory
            if (Directory.Exists(flightMailPath))
            {
                _basePath = flightMailPath;
            }
            else if (Directory.Exists(flightPlanPath))
            {
                // We can use or share FlightPlan path or create MailDirector
                _basePath = flightMailPath;
            }
            else
            {
                _basePath = flightMailPath;
            }
        }

        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
    }

    public string GetBasePath() => _basePath;

    public string GetConfigPath() => Path.Combine(_basePath, "config.json");

    public string GetRulesDirectory()
    {
        var path = Path.Combine(_basePath, "Rules");
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        return path;
    }

    public string GetEmailPreferencesPath() => Path.Combine(_basePath, "email_preferences.json");

    public string GetAuthRecordPath() => Path.Combine(_basePath, "auth_record_flightmail.bin");
}
