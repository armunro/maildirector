using System;
using System.Threading.Tasks;
using System.Windows.Input;
using MailDirector.Models;
using MailDirector.Services;

namespace MailDirector.ViewModels;

public class SettingsViewModel : BaseViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;

    private string _tenantId = string.Empty;
    private string _clientId = string.Empty;
    private bool _demoMode;
    private int _pageSize = 50;
    private int _refreshIntervalSeconds = 120;
    private bool _autoRefreshEnabled = true;
    private string _readingPaneLayout = "Right";
    private string? _statusMessage;

    public string TenantId
    {
        get => _tenantId;
        set => SetProperty(ref _tenantId, value);
    }

    public string ClientId
    {
        get => _clientId;
        set => SetProperty(ref _clientId, value);
    }

    public bool DemoMode
    {
        get => _demoMode;
        set => SetProperty(ref _demoMode, value);
    }

    public int PageSize
    {
        get => _pageSize;
        set => SetProperty(ref _pageSize, value);
    }

    public int RefreshIntervalSeconds
    {
        get => _refreshIntervalSeconds;
        set => SetProperty(ref _refreshIntervalSeconds, value);
    }

    public bool AutoRefreshEnabled
    {
        get => _autoRefreshEnabled;
        set => SetProperty(ref _autoRefreshEnabled, value);
    }

    public string ReadingPaneLayout
    {
        get => _readingPaneLayout;
        set => SetProperty(ref _readingPaneLayout, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string ConfigPath => _storageService.GetConfigPath();
    public string RulesDirectory => _storageService.GetRulesDirectory();
    public string EmailPreferencesPath => _storageService.GetEmailPreferencesPath();

    public ICommand SaveCommand { get; }
    public ICommand ResetDefaultsCommand { get; }

    public event EventHandler<bool>? RequestClose;

    public SettingsViewModel(ISettingsService settingsService, IStorageService storageService)
    {
        _settingsService = settingsService;
        _storageService = storageService;

        LoadSettings();

        SaveCommand = new RelayCommand(Save);
        ResetDefaultsCommand = new RelayCommand(ResetDefaults);
    }

    public void LoadSettings()
    {
        var cfg = _settingsService.Config;
        TenantId = cfg.MicrosoftGraph.TenantId;
        ClientId = cfg.MicrosoftGraph.ClientId;
        DemoMode = cfg.Debug.DemoMode;
        PageSize = cfg.EmailPreferences.PageSize > 0 ? cfg.EmailPreferences.PageSize : 50;
        RefreshIntervalSeconds = cfg.EmailPreferences.RefreshIntervalSeconds > 0 ? cfg.EmailPreferences.RefreshIntervalSeconds : 120;
        AutoRefreshEnabled = cfg.EmailPreferences.AutoRefreshEnabled;
        ReadingPaneLayout = !string.IsNullOrEmpty(cfg.EmailPreferences.ReadingPaneLayout) ? cfg.EmailPreferences.ReadingPaneLayout : "Right";
    }

    private void Save()
    {
        var cfg = _settingsService.Config;
        cfg.MicrosoftGraph.TenantId = TenantId.Trim();
        cfg.MicrosoftGraph.ClientId = ClientId.Trim();
        cfg.Debug.DemoMode = DemoMode;
        cfg.EmailPreferences.PageSize = PageSize;
        cfg.EmailPreferences.RefreshIntervalSeconds = RefreshIntervalSeconds;
        cfg.EmailPreferences.AutoRefreshEnabled = AutoRefreshEnabled;
        cfg.EmailPreferences.ReadingPaneLayout = ReadingPaneLayout;

        _settingsService.Save(true);
        StatusMessage = "Settings saved successfully.";
        RequestClose?.Invoke(this, true);
    }

    private void ResetDefaults()
    {
        DemoMode = false;
        PageSize = 50;
        RefreshIntervalSeconds = 120;
        AutoRefreshEnabled = true;
        ReadingPaneLayout = "Right";
    }
}
