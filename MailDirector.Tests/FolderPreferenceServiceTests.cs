using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MailDirector.Models;
using MailDirector.Services;
using Xunit;

namespace MailDirector.Tests;

public class FolderPreferenceServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly StorageService _storageService;
    private readonly SettingsService _settingsService;
    private readonly FolderPreferenceService _folderPreferenceService;

    public FolderPreferenceServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FlightMail_FolderTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        _storageService = new StorageService(_testDir);
        _settingsService = new SettingsService(_storageService);
        _folderPreferenceService = new FolderPreferenceService(_settingsService);
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
    public void BuildFolderTree_StandardOrder_SortsInboxArchiveSentFirst()
    {
        var raw = new List<MailFolderDto>
        {
            new MailFolderDto("zebra", "Zebra", 0, 0),
            new MailFolderDto("sentitems", "Sent Items", 0, 0),
            new MailFolderDto("inbox", "Inbox", 0, 0),
            new MailFolderDto("archive", "Archive", 0, 0),
            new MailFolderDto("alpha", "Alpha", 0, 0),
        };

        var tree = _folderPreferenceService.BuildFolderTree(raw);

        Assert.Equal("inbox", tree[0].Id);
        Assert.Equal("archive", tree[1].Id);
        Assert.Equal("sentitems", tree[2].Id);
        Assert.Equal("alpha", tree[3].Id);
        Assert.Equal("zebra", tree[4].Id);
    }

    [Fact]
    public void BuildFolderTree_HierarchyLevels_IndentsChildrenProperly()
    {
        var raw = new List<MailFolderDto>
        {
            new MailFolderDto("inbox", "Inbox", 0, 0),
            new MailFolderDto("parent", "Projects", 0, 0),
            new MailFolderDto("child1", "Sub Project 1", 0, 0, "parent", 0),
            new MailFolderDto("child2", "Sub Project 2", 0, 0, "parent", 0),
        };

        var tree = _folderPreferenceService.BuildFolderTree(raw);

        var parent = tree.First(f => f.Id == "parent");
        var child1 = tree.First(f => f.Id == "child1");
        var child2 = tree.First(f => f.Id == "child2");

        Assert.Equal(0, parent.Level);
        Assert.Equal(1, child1.Level);
        Assert.Equal(1, child2.Level);

        // Verify child1 and child2 appear immediately following parent
        var parentIdx = tree.IndexOf(parent);
        Assert.Equal(parentIdx + 1, tree.IndexOf(child1));
        Assert.Equal(parentIdx + 2, tree.IndexOf(child2));
    }

    [Fact]
    public void UpdateFolderCustomName_And_Color_And_Icon_Persists()
    {
        _folderPreferenceService.UpdateFolderCustomName("inbox", "Primary Inbox");
        _folderPreferenceService.UpdateFolderColor("inbox", "#FF0000");
        _folderPreferenceService.UpdateFolderIcon("inbox", "MailInbox24");

        var raw = new List<MailFolderDto>
        {
            new MailFolderDto("inbox", "Inbox", 5, 2)
        };

        var tree = _folderPreferenceService.BuildFolderTree(raw);

        Assert.Equal("Primary Inbox", tree[0].CustomDisplayName);
        Assert.Equal("Primary Inbox", tree[0].EffectiveDisplayName);
        Assert.Equal("#FF0000", tree[0].Color);
        Assert.Equal("MailInbox24", tree[0].CustomIcon);

        // Verify persistence across new service instance
        var newService = new FolderPreferenceService(_settingsService);
        var newTree = newService.BuildFolderTree(raw);
        Assert.Equal("Primary Inbox", newTree[0].EffectiveDisplayName);
    }

    [Fact]
    public void ToggleFolderVisibility_HidesWhenNotEditing()
    {
        var raw = new List<MailFolderDto>
        {
            new MailFolderDto("inbox", "Inbox", 0, 0),
            new MailFolderDto("junk", "Junk Email", 0, 0)
        };

        _folderPreferenceService.ToggleFolderVisibility("junk");

        // Normal view: hidden folders omitted
        var normalTree = _folderPreferenceService.BuildFolderTree(raw, showHidden: false);
        Assert.Single(normalTree);
        Assert.Equal("inbox", normalTree[0].Id);

        // Edit view: hidden folders included (so user can unhide them)
        var editTree = _folderPreferenceService.BuildFolderTree(raw, showHidden: true);
        Assert.Equal(2, editTree.Count);
        Assert.True(editTree.First(f => f.Id == "junk").IsHidden);
    }

    [Fact]
    public void MoveFolder_ReordersFolders()
    {
        var raw = new List<MailFolderDto>
        {
            new MailFolderDto("folder1", "First", 0, 0),
            new MailFolderDto("folder2", "Second", 0, 0),
            new MailFolderDto("folder3", "Third", 0, 0),
        };

        var current = _folderPreferenceService.BuildFolderTree(raw);

        // Move folder3 up 1
        _folderPreferenceService.MoveFolder("folder3", -1, current);

        var reordered = _folderPreferenceService.BuildFolderTree(raw);
        Assert.Equal("folder1", reordered[0].Id);
        Assert.Equal("folder3", reordered[1].Id);
        Assert.Equal("folder2", reordered[2].Id);
    }
}
