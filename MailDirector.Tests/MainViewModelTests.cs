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
}
