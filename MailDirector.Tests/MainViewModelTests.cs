using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MailDirector.Models;
using MailDirector.Services;
using MailDirector.ViewModels;
using Xunit;

namespace MailDirector.Tests;

public class MainViewModelTests : IDisposable
{
    private readonly string _testDir;
    private readonly StorageService _storageService;
    private readonly SettingsService _settingsService;
    private readonly FolderPreferenceService _folderPreferenceService;
    private readonly MockEmailService _emailService;
    private readonly RuleService _ruleService;
    private readonly MainViewModel _mainViewModel;

    public MainViewModelTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FlightMail_VMTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        _storageService = new StorageService(_testDir);
        _settingsService = new SettingsService(_storageService);
        _folderPreferenceService = new FolderPreferenceService(_settingsService);
        _emailService = new MockEmailService();
        _ruleService = new RuleService(_emailService, _storageService);

        _mainViewModel = new MainViewModel(
            _emailService,
            _ruleService,
            _folderPreferenceService,
            _settingsService);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task InitializeAsync_LoadsFoldersAndEmails()
    {
        await _mainViewModel.InitializeAsync();

        Assert.NotEmpty(_mainViewModel.Folders);
        Assert.NotNull(_mainViewModel.SelectedFolder);
        Assert.Equal("inbox", _mainViewModel.SelectedFolder.Id);

        Assert.NotEmpty(_mainViewModel.Emails);
        Assert.NotNull(_mainViewModel.SelectedEmail);
    }

    [Fact]
    public async Task SearchText_FiltersEmailsCorrectly()
    {
        await _mainViewModel.InitializeAsync();

        var total = _mainViewModel.Emails.Count;
        Assert.True(total > 0);

        _mainViewModel.SearchText = "Jira";
        Assert.All(_mainViewModel.Emails, e =>
            Assert.True(
                e.Subject.Contains("Jira", StringComparison.OrdinalIgnoreCase) ||
                e.From.Contains("Jira", StringComparison.OrdinalIgnoreCase) ||
                e.BodyPreview.Contains("Jira", StringComparison.OrdinalIgnoreCase) ||
                e.MatchingRules.Any(r => r.Name.Contains("Jira", StringComparison.OrdinalIgnoreCase))));

        _mainViewModel.SearchText = string.Empty;
        Assert.Equal(total, _mainViewModel.Emails.Count);
    }

    [Fact]
    public async Task DeleteEmailCommand_RemovesEmailFromCollection()
    {
        await _mainViewModel.InitializeAsync();
        var selected = _mainViewModel.SelectedEmail;
        Assert.NotNull(selected);

        _mainViewModel.DeleteEmailCommand.Execute(null);

        // Wait a tick for async command execution
        await Task.Delay(50);

        Assert.DoesNotContain(_mainViewModel.Emails, e => e.Id == selected.Id);
    }

    [Fact]
    public async Task ArchiveEmailCommand_MovesEmailToArchive()
    {
        await _mainViewModel.InitializeAsync();
        var selected = _mainViewModel.SelectedEmail;
        Assert.NotNull(selected);

        _mainViewModel.ArchiveEmailCommand.Execute(null);

        await Task.Delay(50);

        Assert.DoesNotContain(_mainViewModel.Emails, e => e.Id == selected.Id);
    }

    [Fact]
    public void ReadingPaneLayout_UpdatesAndSavesPreference()
    {
        _mainViewModel.ReadingPaneLayout = "Bottom";

        Assert.True(_mainViewModel.IsReadingPaneBottom);
        Assert.False(_mainViewModel.IsReadingPaneRight);
        Assert.True(_mainViewModel.IsReadingPaneVisible);

        _mainViewModel.ReadingPaneLayout = "Off";
        Assert.False(_mainViewModel.IsReadingPaneVisible);
    }

    [Fact]
    public void BodyPreview_JoinedToSingleLine()
    {
        var item = new EmailItem
        {
            BodyPreview = "Line 1\r\nLine 2\n\tLine 3   with   spaces\rLine 4"
        };

        Assert.Equal("Line 1 Line 2 Line 3 with spaces Line 4", item.BodyPreview);

        var converter = new MailDirector.Converters.SingleLineConverter();
        var converted = converter.Convert("Line A\r\nLine B\nLine C", typeof(string), null, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal("Line A Line B Line C", converted);
    }

    [Fact]
    public void DisplayBody_ReturnsBody_WhenBodyPresent()
    {
        var item = new EmailItem
        {
            BodyPreview = "Short preview",
            Body = "<div><p>Full email html content</p></div>"
        };

        Assert.Equal("<div><p>Full email html content</p></div>", item.DisplayBody);
    }

    [Fact]
    public void DisplayBody_FallsBackToBodyPreview_WhenBodyNullOrEmpty()
    {
        var item = new EmailItem
        {
            BodyPreview = "Short preview only",
            Body = null
        };

        Assert.Equal("Short preview only", item.DisplayBody);

        item.Body = "   ";
        Assert.Equal("Short preview only", item.DisplayBody);
    }

    [Fact]
    public void PrepareHtml_FormatsHtmlAndPlainTextCorrectly()
    {
        var plainResult = MailDirector.Helpers.WebBrowserHelper.PrepareHtml("Hello World\nLine 2");
        Assert.Contains("Hello World<br/>Line 2", plainResult);
        Assert.Contains("IE=edge", plainResult);
        Assert.Contains("#ffffff", plainResult);

        var plainDarkResult = MailDirector.Helpers.WebBrowserHelper.PrepareHtml("Hello World\nLine 2", isDarkMode: true);
        Assert.Contains("Hello World<br/>Line 2", plainDarkResult);
        Assert.Contains("IE=edge", plainDarkResult);
        Assert.Contains("#1e1e1e", plainDarkResult);

        var htmlSnippet = "<div><h3>Title</h3><p>Paragraph</p></div>";
        var htmlResult = MailDirector.Helpers.WebBrowserHelper.PrepareHtml(htmlSnippet, isDarkMode: false);
        Assert.Contains(htmlSnippet, htmlResult);
        Assert.Contains("IE=edge", htmlResult);
        Assert.Contains("#ffffff", htmlResult);

        var htmlDarkResult = MailDirector.Helpers.WebBrowserHelper.PrepareHtml(htmlSnippet, isDarkMode: true);
        Assert.Contains(htmlSnippet, htmlDarkResult);
        Assert.Contains("IE=edge", htmlDarkResult);
        Assert.Contains("#1e1e1e", htmlDarkResult);

        var fullHtml = "<html><head><title>Test</title></head><body><h1>Content</h1></body></html>";
        var fullHtmlResult = MailDirector.Helpers.WebBrowserHelper.PrepareHtml(fullHtml, isDarkMode: false);
        Assert.Contains("IE=edge", fullHtmlResult);
        Assert.Contains("<h1>Content</h1>", fullHtmlResult);

        var fullHtmlDarkResult = MailDirector.Helpers.WebBrowserHelper.PrepareHtml(fullHtml, isDarkMode: true);
        Assert.Contains("IE=edge", fullHtmlDarkResult);
        Assert.Contains("<h1>Content</h1>", fullHtmlDarkResult);
        Assert.Contains("#1e1e1e !important", fullHtmlDarkResult);
        Assert.Contains("table, thead, tbody, tfoot, tr, td, th, div", fullHtmlDarkResult);
        Assert.Contains("::-webkit-scrollbar", fullHtmlDarkResult);
        Assert.Contains("scrollbar-face-color", fullHtmlDarkResult);
    }

    [Fact]
    public void BoolToVisibilityConverter_SupportsUseHiddenAndInverse()
    {
        var conv = new MailDirector.Converters.BoolToVisibilityConverter();
        Assert.Equal(System.Windows.Visibility.Visible, conv.Convert(true, typeof(System.Windows.Visibility), null, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(System.Windows.Visibility.Collapsed, conv.Convert(false, typeof(System.Windows.Visibility), null, System.Globalization.CultureInfo.InvariantCulture));

        var inverseHidden = new MailDirector.Converters.BoolToVisibilityConverter { Inverse = true, UseHidden = true };
        Assert.Equal(System.Windows.Visibility.Hidden, inverseHidden.Convert(true, typeof(System.Windows.Visibility), null, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(System.Windows.Visibility.Visible, inverseHidden.Convert(false, typeof(System.Windows.Visibility), null, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void IsReadingPaneDarkMode_TogglesAndPersistsPreference()
    {
        Assert.True(_mainViewModel.IsReadingPaneDarkMode);

        _mainViewModel.ToggleReadingPaneDarkModeCommand.Execute(null);
        Assert.False(_mainViewModel.IsReadingPaneDarkMode);
        Assert.False(_settingsService.Config.EmailPreferences.DarkModeReadingPane);

        _mainViewModel.IsReadingPaneDarkMode = true;
        Assert.True(_mainViewModel.IsReadingPaneDarkMode);
        Assert.True(_settingsService.Config.EmailPreferences.DarkModeReadingPane);
    }

    [Fact]
    public async Task HasSelectedEmail_TracksSelectedEmailState()
    {
        _mainViewModel.SelectedEmail = null;
        Assert.False(_mainViewModel.HasSelectedEmail);
        Assert.False(_mainViewModel.DeleteEmailCommand.CanExecute(null));
        Assert.False(_mainViewModel.ArchiveEmailCommand.CanExecute(null));
        Assert.False(_mainViewModel.ToggleReadCommand.CanExecute(null));
        Assert.False(_mainViewModel.ToggleStarCommand.CanExecute(null));

        await _mainViewModel.InitializeAsync();
        Assert.NotNull(_mainViewModel.SelectedEmail);
        Assert.True(_mainViewModel.HasSelectedEmail);
        Assert.True(_mainViewModel.DeleteEmailCommand.CanExecute(null));
        Assert.True(_mainViewModel.ArchiveEmailCommand.CanExecute(null));
        Assert.True(_mainViewModel.ToggleReadCommand.CanExecute(null));
        Assert.True(_mainViewModel.ToggleStarCommand.CanExecute(null));

        _mainViewModel.SelectedEmail = null;
        Assert.False(_mainViewModel.HasSelectedEmail);
    }

    [Fact]
    public async Task ApplyRuleToAllCommand_ExecutesRuleAcrossSelectedFolder()
    {
        await _mainViewModel.InitializeAsync();
        Assert.NotNull(_mainViewModel.SelectedFolder);

        var rule = new FilterRule
        {
            Name = "MarkAllReadInFolder",
            RootFolder = "Inbox",
            Filters = new List<FilterCriteria> { new FilterCriteria { From = new List<string> { "jira" } } },
            Actions = new List<RuleAction> { new RuleAction { Type = ActionType.MarkAsRead } }
        };
        await _ruleService.SaveRuleAsync(rule);
        _mainViewModel.LoadRules();

        Assert.True(_mainViewModel.ApplyRuleToAllCommand.CanExecute("MarkAllReadInFolder"));
        _mainViewModel.ApplyRuleToAllCommand.Execute("MarkAllReadInFolder");

        await Task.Delay(100);

        Assert.Contains("successfully processed", _mainViewModel.StatusMessage);
    }

    [Fact]
    public async Task MarkFolderAsReadCommand_MarksAllEmailsInFolderAsRead()
    {
        await _mainViewModel.InitializeAsync();
        var inbox = _mainViewModel.Folders.First(f => f.Id.Equals("inbox", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(inbox);
        Assert.True(inbox.UnreadItemCount > 0);

        await _mainViewModel.LoadEmailsForFolderAsync(inbox.Id);
        Assert.Contains(_mainViewModel.Emails, e => !e.IsRead);

        var task = _mainViewModel.MarkFolderAsReadAsync(inbox);
        await task;

        Assert.Equal(0, inbox.UnreadItemCount);
        Assert.All(_mainViewModel.Emails, e => Assert.True(e.IsRead));
        Assert.Contains("marked as read", _mainViewModel.StatusMessage);
        Assert.False(_mainViewModel.IsApplyingRule);
    }

    [Fact]
    public async Task MarkFolderAsReadCommand_MarksNonVisibleEmailsAsRead()
    {
        await _mainViewModel.InitializeAsync();
        var inbox = _mainViewModel.Folders.First(f => f.Id.Equals("inbox", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(inbox);

        // Set search text to filter out emails or load a different folder
        _mainViewModel.SearchText = "NonExistentSubjectSearchFilterXYZ";
        Assert.Empty(_mainViewModel.Emails);

        var unreadBefore = (await _emailService.GetUnreadEmailsAsync(inbox.Id)).ToList();
        Assert.NotEmpty(unreadBefore);

        await _mainViewModel.MarkFolderAsReadAsync(inbox);

        var unreadAfter = (await _emailService.GetUnreadEmailsAsync(inbox.Id)).ToList();
        Assert.Empty(unreadAfter);
        Assert.Equal(0, inbox.UnreadItemCount);
    }

    [Fact]
    public void ProgressProperties_FormatProperly()
    {
        _mainViewModel.ApplyingRuleName = "TestRule";
        Assert.Equal("Applying Rule: TestRule", _mainViewModel.ProgressTitle);

        _mainViewModel.ProgressTitle = "Marking as Read: Inbox";
        Assert.Equal("Marking as Read: Inbox", _mainViewModel.ProgressTitle);

        _mainViewModel.RuleProgressCurrent = 2;
        _mainViewModel.RuleProgressTotal = 5;
        Assert.Equal("Processing 2 of 5", _mainViewModel.RuleProgressStatusText);
    }
}
