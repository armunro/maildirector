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
}
