using System.Collections.Generic;

namespace MailDirector.Models;

public class AppConfig
{
    public MicrosoftGraphConfig MicrosoftGraph { get; set; } = new();
    public EmailPreferences EmailPreferences { get; set; } = new();
    public DebugConfig Debug { get; set; } = new();
}

public class MicrosoftGraphConfig
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(ClientId);
}

public class EmailPreferences
{
    public string ReadingPaneLayout { get; set; } = "Right"; // Right, Bottom, Off
    public int PageSize { get; set; } = 50;
    public int RefreshIntervalSeconds { get; set; } = 120;
    public bool AutoRefreshEnabled { get; set; } = true;
    public bool DarkTheme { get; set; } = true;
    public Dictionary<string, FolderPreference> Folders { get; set; } = new();
}

public class DebugConfig
{
    public bool DemoMode { get; set; } = false;
}
