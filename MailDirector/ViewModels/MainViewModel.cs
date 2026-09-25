using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using MailDirector.Models;
using MailDirector.Services;

namespace MailDirector.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly IEmailService _emailService;
    private readonly IRuleService _ruleService;
    private readonly IFolderPreferenceService _folderPreferenceService;
    private readonly ISettingsService _settingsService;
    private readonly DispatcherTimer _refreshTimer;
    private CancellationTokenSource? _ruleCts;

    private ObservableCollection<MailFolderItem> _folders = new();
    private MailFolderItem? _selectedFolder;
    private bool _isEditingFolders;
    private bool _sidebarCollapsed;

    private List<EmailItem> _rawEmails = new();
    private ObservableCollection<EmailItem> _emails = new();
    private EmailItem? _selectedEmail;
    private string _searchText = string.Empty;
    private bool _isLoading;
    private bool _isRefreshing;
    private string? _statusMessage;

    private ObservableCollection<FilterRule> _rules = new();
    private string? _applyingRuleName;
    private bool _isApplyingRule;
    private int _ruleProgressCurrent;
    private int _ruleProgressTotal;
    private string? _ruleProgressSubject;
    private int _ruleProgressPercent;

    private string _readingPaneLayout = "Right"; // Right, Bottom, Off

    // Dialog & Navigation events
    public event EventHandler? RequestOpenRulesManager;
    public event EventHandler<(EmailItem Email, bool AddToExisting)>? RequestOpenCreateRule;
    public event EventHandler? RequestOpenSettings;

    public ObservableCollection<MailFolderItem> Folders
    {
        get => _folders;
        set => SetProperty(ref _folders, value);
    }

    public MailFolderItem? SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (SetProperty(ref _selectedFolder, value))
            {
                if (value != null)
                {
                    _ = LoadEmailsForFolderAsync(value.Id);
                }
            }
        }
    }

    public bool IsEditingFolders
    {
        get => _isEditingFolders;
        set
        {
            if (SetProperty(ref _isEditingFolders, value))
            {
                RefreshFolderTreeDisplay();
            }
        }
    }

    public bool SidebarCollapsed
    {
        get => _sidebarCollapsed;
        set => SetProperty(ref _sidebarCollapsed, value);
    }

    public ObservableCollection<EmailItem> Emails
    {
        get => _emails;
        set => SetProperty(ref _emails, value);
    }

    public EmailItem? SelectedEmail
    {
        get => _selectedEmail;
        set => SetProperty(ref _selectedEmail, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplySearchFilter();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<FilterRule> Rules
    {
        get => _rules;
        set => SetProperty(ref _rules, value);
    }

    public string? ApplyingRuleName
    {
        get => _applyingRuleName;
        set => SetProperty(ref _applyingRuleName, value);
    }

    public bool IsApplyingRule
    {
        get => _isApplyingRule;
        set => SetProperty(ref _isApplyingRule, value);
    }

    public int RuleProgressCurrent
    {
        get => _ruleProgressCurrent;
        set => SetProperty(ref _ruleProgressCurrent, value);
    }

    public int RuleProgressTotal
    {
        get => _ruleProgressTotal;
        set => SetProperty(ref _ruleProgressTotal, value);
    }

    public string? RuleProgressSubject
    {
        get => _ruleProgressSubject;
        set => SetProperty(ref _ruleProgressSubject, value);
    }

    public int RuleProgressPercent
    {
        get => _ruleProgressPercent;
        set => SetProperty(ref _ruleProgressPercent, value);
    }

    public string ReadingPaneLayout
    {
        get => _readingPaneLayout;
        set
        {
            if (SetProperty(ref _readingPaneLayout, value))
            {
                _settingsService.Config.EmailPreferences.ReadingPaneLayout = value;
                _settingsService.SaveDebounced();
                OnPropertyChanged(nameof(IsReadingPaneRight));
                OnPropertyChanged(nameof(IsReadingPaneBottom));
                OnPropertyChanged(nameof(IsReadingPaneVisible));
            }
        }
    }

    public bool IsReadingPaneRight => ReadingPaneLayout == "Right";
    public bool IsReadingPaneBottom => ReadingPaneLayout == "Bottom";
    public bool IsReadingPaneVisible => ReadingPaneLayout != "Off";

    // Commands
    public ICommand RefreshCommand { get; }
    public ICommand ToggleSidebarCommand { get; }
    public ICommand ToggleFolderEditModeCommand { get; }
    public ICommand MoveFolderUpCommand { get; }
    public ICommand MoveFolderDownCommand { get; }
    public ICommand ToggleFolderVisibilityCommand { get; }
    public ICommand SetFolderColorCommand { get; }
    public ICommand SetFolderIconCommand { get; }
    public ICommand ResetFolderCommand { get; }
    public ICommand SaveFolderPreferencesCommand { get; }
    public ICommand SelectFolderCommand { get; }
    public ICommand SelectEmailCommand { get; }
    public ICommand DeleteEmailCommand { get; }
    public ICommand ArchiveEmailCommand { get; }
    public ICommand ToggleReadCommand { get; }
    public ICommand ToggleStarCommand { get; }
    public ICommand OpenInWebCommand { get; }
    public ICommand ApplyRuleCommand { get; }
    public ICommand ApplyRuleToAllCommand { get; }
    public ICommand CancelApplyRuleCommand { get; }
    public ICommand OpenRulesManagerCommand { get; }
    public ICommand OpenCreateRuleDialogCommand { get; }
    public ICommand OpenAddSenderToRuleDialogCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand SetLayoutCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public MainViewModel(
        IEmailService emailService,
        IRuleService ruleService,
        IFolderPreferenceService folderPreferenceService,
        ISettingsService settingsService)
    {
        _emailService = emailService;
        _ruleService = ruleService;
        _folderPreferenceService = folderPreferenceService;
        _settingsService = settingsService;

        _readingPaneLayout = !string.IsNullOrEmpty(_settingsService.Config.EmailPreferences.ReadingPaneLayout)
            ? _settingsService.Config.EmailPreferences.ReadingPaneLayout
            : "Right";

        _ruleService.RulesChanged += (s, e) => LoadRules();

        RefreshCommand = new AsyncRelayCommand(RefreshAllAsync);
        ToggleSidebarCommand = new RelayCommand(() => SidebarCollapsed = !SidebarCollapsed);
        ToggleFolderEditModeCommand = new RelayCommand(ToggleFolderEditMode);
        MoveFolderUpCommand = new RelayCommand<MailFolderItem>(f => MoveFolder(f, -1));
        MoveFolderDownCommand = new RelayCommand<MailFolderItem>(f => MoveFolder(f, 1));
        ToggleFolderVisibilityCommand = new RelayCommand<MailFolderItem>(ToggleFolderVisibility);
        SetFolderColorCommand = new RelayCommand<(MailFolderItem Folder, string Color)>(p => SetFolderColor(p.Folder, p.Color));
        SetFolderIconCommand = new RelayCommand<(MailFolderItem Folder, string Icon)>(p => SetFolderIcon(p.Folder, p.Icon));
        ResetFolderCommand = new RelayCommand<MailFolderItem>(ResetFolder);
        SaveFolderPreferencesCommand = new RelayCommand(SaveFolderPreferences);

        SelectFolderCommand = new RelayCommand<MailFolderItem>(f => { if (f != null) SelectedFolder = f; });
        SelectEmailCommand = new RelayCommand<EmailItem>(e => SelectedEmail = e);
        DeleteEmailCommand = new AsyncRelayCommand(DeleteSelectedEmailAsync, () => SelectedEmail != null);
        ArchiveEmailCommand = new AsyncRelayCommand(ArchiveSelectedEmailAsync, () => SelectedEmail != null);
        ToggleReadCommand = new AsyncRelayCommand(ToggleReadSelectedEmailAsync, () => SelectedEmail != null);
        ToggleStarCommand = new AsyncRelayCommand(ToggleStarSelectedEmailAsync, () => SelectedEmail != null);
        OpenInWebCommand = new RelayCommand(OpenSelectedEmailInWeb, () => SelectedEmail != null && !string.IsNullOrEmpty(SelectedEmail.WebLink));

        ApplyRuleCommand = new AsyncRelayCommand<string>(ApplyRuleToSelectedEmailAsync, _ => SelectedEmail != null);
        ApplyRuleToAllCommand = new AsyncRelayCommand<string>(ApplyRuleToAllInFolderAsync);
        CancelApplyRuleCommand = new RelayCommand(CancelApplyRule);

        OpenRulesManagerCommand = new RelayCommand(() => RequestOpenRulesManager?.Invoke(this, EventArgs.Empty));
        OpenCreateRuleDialogCommand = new RelayCommand(() =>
        {
            if (SelectedEmail != null)
                RequestOpenCreateRule?.Invoke(this, (SelectedEmail, false));
        }, () => SelectedEmail != null);

        OpenAddSenderToRuleDialogCommand = new RelayCommand(() =>
        {
            if (SelectedEmail != null)
                RequestOpenCreateRule?.Invoke(this, (SelectedEmail, true));
        }, () => SelectedEmail != null);

        OpenSettingsCommand = new RelayCommand(() => RequestOpenSettings?.Invoke(this, EventArgs.Empty));
        SetLayoutCommand = new RelayCommand<string>(layout => { if (!string.IsNullOrEmpty(layout)) ReadingPaneLayout = layout; });
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);

        // Auto refresh timer
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(30, _settingsService.Config.EmailPreferences.RefreshIntervalSeconds))
        };
        _refreshTimer.Tick += async (s, e) =>
        {
            if (_settingsService.Config.EmailPreferences.AutoRefreshEnabled && !IsLoading && !IsRefreshing && !IsApplyingRule)
            {
                await RefreshCurrentFolderSilentlyAsync();
            }
        };
        _refreshTimer.Start();
    }

    public async Task InitializeAsync()
    {
        LoadRules();
        await LoadFoldersAsync();
    }

    public void LoadRules()
    {
        var rules = _ruleService.GetAllRules();
        Rules = new ObservableCollection<FilterRule>(rules);
        UpdateEmailMatchingRules();
    }

    private IEnumerable<MailFolderDto> _rawFolderDtos = Enumerable.Empty<MailFolderDto>();

    public async Task LoadFoldersAsync()
    {
        try
        {
            IsLoading = true;
            _rawFolderDtos = await _emailService.GetMailFoldersAsync();
            RefreshFolderTreeDisplay();

            if (SelectedFolder == null && Folders.Count > 0)
            {
                var inbox = Folders.FirstOrDefault(f => f.Id.Equals("inbox", StringComparison.OrdinalIgnoreCase)) ?? Folders[0];
                SelectedFolder = inbox;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading folders: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RefreshFolderTreeDisplay()
    {
        var currentSelectedId = SelectedFolder?.Id;
        var tree = _folderPreferenceService.BuildFolderTree(_rawFolderDtos, IsEditingFolders);
        Folders = new ObservableCollection<MailFolderItem>(tree);

        if (!string.IsNullOrEmpty(currentSelectedId))
        {
            _selectedFolder = Folders.FirstOrDefault(f => f.Id.Equals(currentSelectedId, StringComparison.OrdinalIgnoreCase));
            OnPropertyChanged(nameof(SelectedFolder));
        }
    }

    public async Task LoadEmailsForFolderAsync(string folderId)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading emails...";

            var pageSize = _settingsService.Config.EmailPreferences.PageSize > 0
                ? _settingsService.Config.EmailPreferences.PageSize
                : 50;

            var emailDtos = await _emailService.GetEmailsAsync(folderId, pageSize);
            var rules = _ruleService.GetAllRules();

            _rawEmails = emailDtos.Select(dto =>
            {
                var matching = rules
                    .Where(r => _ruleService.Matches(r, dto))
                    .Select(r => new MatchingRuleDto(r.Name, r.Color))
                    .ToList();
                return EmailItem.FromDto(dto, matching);
            }).ToList();

            ApplySearchFilter();

            if (Emails.Count > 0)
            {
                SelectedEmail = Emails[0];
            }
            else
            {
                SelectedEmail = null;
            }

            StatusMessage = $"{Emails.Count} email(s) loaded.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error fetching emails: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplySearchFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            Emails = new ObservableCollection<EmailItem>(_rawEmails);
        }
        else
        {
            var query = SearchText.Trim();
            var filtered = _rawEmails.Where(e =>
                (!string.IsNullOrEmpty(e.Subject) && e.Subject.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.From) && e.From.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.FromAddress) && e.FromAddress.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.BodyPreview) && e.BodyPreview.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                e.MatchingRules.Any(r => r.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            ).ToList();

            Emails = new ObservableCollection<EmailItem>(filtered);
        }

        if (SelectedEmail != null && !Emails.Contains(SelectedEmail))
        {
            SelectedEmail = Emails.FirstOrDefault();
        }
    }

    private void UpdateEmailMatchingRules()
    {
        var rules = _ruleService.GetAllRules();
        foreach (var email in _rawEmails)
        {
            var dto = new EmailDto(
                email.Id,
                email.From,
                email.FromAddress,
                email.Subject,
                email.BodyPreview,
                email.ReceivedDateTime,
                email.WebLink,
                email.Body,
                null,
                email.IsRead,
                email.IsFlagged
            );

            var matching = rules
                .Where(r => _ruleService.Matches(r, dto))
                .Select(r => new MatchingRuleDto(r.Name, r.Color))
                .ToList();

            email.MatchingRules.Clear();
            foreach (var m in matching)
            {
                email.MatchingRules.Add(m);
            }
        }
    }

    public async Task RefreshAllAsync()
    {
        try
        {
            IsRefreshing = true;
            LoadRules();
            await LoadFoldersAsync();
            if (SelectedFolder != null)
            {
                await LoadEmailsForFolderAsync(SelectedFolder.Id);
            }
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task RefreshCurrentFolderSilentlyAsync()
    {
        if (SelectedFolder == null) return;
        try
        {
            var pageSize = _settingsService.Config.EmailPreferences.PageSize > 0 ? _settingsService.Config.EmailPreferences.PageSize : 50;
            var emailDtos = await _emailService.GetEmailsAsync(SelectedFolder.Id, pageSize);
            var rules = _ruleService.GetAllRules();

            var currentId = SelectedEmail?.Id;

            _rawEmails = emailDtos.Select(dto =>
            {
                var matching = rules
                    .Where(r => _ruleService.Matches(r, dto))
                    .Select(r => new MatchingRuleDto(r.Name, r.Color))
                    .ToList();
                return EmailItem.FromDto(dto, matching);
            }).ToList();

            ApplySearchFilter();

            if (!string.IsNullOrEmpty(currentId))
            {
                var match = Emails.FirstOrDefault(e => e.Id == currentId);
                if (match != null) SelectedEmail = match;
            }
        }
        catch { }
    }

    // Folder Customization Methods
    private void ToggleFolderEditMode()
    {
        IsEditingFolders = !IsEditingFolders;
        if (!IsEditingFolders)
        {
            SaveFolderPreferences();
        }
    }

    private void MoveFolder(MailFolderItem? folder, int direction)
    {
        if (folder == null) return;
        _folderPreferenceService.MoveFolder(folder.Id, direction, Folders);
        RefreshFolderTreeDisplay();
    }

    private void ToggleFolderVisibility(MailFolderItem? folder)
    {
        if (folder == null) return;
        _folderPreferenceService.ToggleFolderVisibility(folder.Id);
        RefreshFolderTreeDisplay();
    }

    public void SetFolderColor(MailFolderItem? folder, string color)
    {
        if (folder == null) return;
        _folderPreferenceService.UpdateFolderColor(folder.Id, color);
        RefreshFolderTreeDisplay();
    }

    public void SetFolderIcon(MailFolderItem? folder, string icon)
    {
        if (folder == null) return;
        _folderPreferenceService.UpdateFolderIcon(folder.Id, icon);
        RefreshFolderTreeDisplay();
    }

    public void RenameFolder(MailFolderItem? folder, string newName)
    {
        if (folder == null) return;
        _folderPreferenceService.UpdateFolderCustomName(folder.Id, newName);
        RefreshFolderTreeDisplay();
    }

    private void ResetFolder(MailFolderItem? folder)
    {
        if (folder == null) return;
        _folderPreferenceService.ResetFolder(folder.Id);
        RefreshFolderTreeDisplay();
    }

    private void SaveFolderPreferences()
    {
        // Capture any edited custom names from the view models
        foreach (var f in Folders)
        {
            if (!string.IsNullOrWhiteSpace(f.CustomDisplayName) && !f.CustomDisplayName.Equals(f.DisplayName, StringComparison.OrdinalIgnoreCase))
            {
                _folderPreferenceService.UpdateFolderCustomName(f.Id, f.CustomDisplayName);
            }
        }
        _folderPreferenceService.Save();
    }

    // Email Action Methods
    private async Task DeleteSelectedEmailAsync()
    {
        if (SelectedEmail == null) return;
        var email = SelectedEmail;

        try
        {
            await _emailService.MoveEmailToDeletedItemsAsync(email.Id);
            _rawEmails.Remove(email);
            Emails.Remove(email);
            SelectedEmail = Emails.FirstOrDefault();
            StatusMessage = "Email moved to Deleted Items.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting email: {ex.Message}";
        }
    }

    private async Task ArchiveSelectedEmailAsync()
    {
        if (SelectedEmail == null) return;
        var email = SelectedEmail;

        try
        {
            await _emailService.MoveEmailToFolderAsync(email.Id, "archive");
            _rawEmails.Remove(email);
            Emails.Remove(email);
            SelectedEmail = Emails.FirstOrDefault();
            StatusMessage = "Email archived.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error archiving email: {ex.Message}";
        }
    }

    private async Task ToggleReadSelectedEmailAsync()
    {
        if (SelectedEmail == null) return;
        var email = SelectedEmail;
        email.IsRead = !email.IsRead;

        try
        {
            var action = email.IsRead
                ? new RuleAction { Type = ActionType.MarkAsRead }
                : new RuleAction { Type = ActionType.MarkAsRead }; // Can be extended for unread
            await _emailService.ApplyRuleActionsAsync(email.Id, new List<RuleAction> { action });
        }
        catch { }
    }

    private async Task ToggleStarSelectedEmailAsync()
    {
        if (SelectedEmail == null) return;
        var email = SelectedEmail;
        email.IsFlagged = !email.IsFlagged;

        try
        {
            var action = email.IsFlagged
                ? new RuleAction { Type = ActionType.Star }
                : new RuleAction { Type = ActionType.ClearFlag };
            await _emailService.ApplyRuleActionsAsync(email.Id, new List<RuleAction> { action });
        }
        catch { }
    }

    private void OpenSelectedEmailInWeb()
    {
        if (SelectedEmail == null || string.IsNullOrEmpty(SelectedEmail.WebLink)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SelectedEmail.WebLink,
                UseShellExecute = true
            });
        }
        catch { }
    }

    // Rule Application Methods
    private async Task ApplyRuleToSelectedEmailAsync(string? ruleName)
    {
        if (SelectedEmail == null || string.IsNullOrWhiteSpace(ruleName)) return;

        try
        {
            StatusMessage = $"Applying rule '{ruleName}'...";
            await _ruleService.ApplyRuleAsync(SelectedEmail.Id, ruleName);
            StatusMessage = $"Rule '{ruleName}' applied.";
            if (SelectedFolder != null)
            {
                await LoadEmailsForFolderAsync(SelectedFolder.Id);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error applying rule: {ex.Message}";
        }
    }

    private async Task ApplyRuleToAllInFolderAsync(string? ruleName)
    {
        if (SelectedFolder == null || string.IsNullOrWhiteSpace(ruleName)) return;

        IsApplyingRule = true;
        ApplyingRuleName = ruleName;
        RuleProgressCurrent = 0;
        RuleProgressTotal = Emails.Count;
        RuleProgressPercent = 0;
        RuleProgressSubject = string.Empty;

        _ruleCts = new CancellationTokenSource();

        var progress = new Progress<RuleProgressReport>(report =>
        {
            RuleProgressCurrent = report.Current;
            RuleProgressTotal = report.Total;
            RuleProgressSubject = report.Subject;
            RuleProgressPercent = report.Total > 0 ? (int)((double)report.Current / report.Total * 100) : 0;
        });

        try
        {
            await _ruleService.ApplyRuleToFolderAsync(SelectedFolder.Id, ruleName, progress, _ruleCts.Token);
            StatusMessage = $"Rule '{ruleName}' successfully processed across {SelectedFolder.EffectiveDisplayName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error running rule: {ex.Message}";
        }
        finally
        {
            IsApplyingRule = false;
            ApplyingRuleName = null;
            if (SelectedFolder != null)
            {
                await LoadEmailsForFolderAsync(SelectedFolder.Id);
            }
        }
    }

    private void CancelApplyRule()
    {
        _ruleCts?.Cancel();
        IsApplyingRule = false;
        ApplyingRuleName = null;
    }
}
