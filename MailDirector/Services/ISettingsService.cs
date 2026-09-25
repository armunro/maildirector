using System;
using System.Collections.Generic;
using MailDirector.Models;

namespace MailDirector.Services;

public interface ISettingsService
{
    AppConfig Config { get; }
    void Load();
    void Save(bool raiseConfigChanged = true);
    void SaveDebounced(int delayMs = 400, bool raiseConfigChanged = false);
    Dictionary<string, FolderPreference> LoadFolderPreferences();
    void SaveFolderPreferences(Dictionary<string, FolderPreference> preferences);
    event EventHandler? ConfigChanged;
}
