using System;
using System.Collections.Generic;
using System.Linq;
using MailDirector.Models;

namespace MailDirector.Services;

public class FolderPreferenceService : IFolderPreferenceService
{
    private readonly ISettingsService _settingsService;
    private readonly Dictionary<string, FolderPreference> _preferences = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, FolderPreference> Preferences => _preferences;

    public FolderPreferenceService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        Load();
    }

    public void Load()
    {
        _preferences.Clear();
        var loaded = _settingsService.LoadFolderPreferences();
        if (loaded != null)
        {
            foreach (var kvp in loaded)
            {
                _preferences[kvp.Key] = kvp.Value;
            }
        }
    }

    public void Save()
    {
        _settingsService.SaveFolderPreferences(_preferences);
    }

    private FolderPreference GetOrCreatePref(string folderId)
    {
        if (!_preferences.TryGetValue(folderId, out var pref))
        {
            pref = new FolderPreference();
            _preferences[folderId] = pref;
        }
        return pref;
    }

    public void UpdateFolderCustomName(string folderId, string? customName)
    {
        var pref = GetOrCreatePref(folderId);
        pref.CustomName = string.IsNullOrWhiteSpace(customName) ? null : customName.Trim();
        Save();
    }

    public void UpdateFolderIcon(string folderId, string? icon)
    {
        var pref = GetOrCreatePref(folderId);
        pref.CustomIcon = string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();
        Save();
    }

    public void UpdateFolderColor(string folderId, string? color)
    {
        var pref = GetOrCreatePref(folderId);
        pref.Color = string.IsNullOrWhiteSpace(color) ? null : color.Trim();
        Save();
    }

    public void ToggleFolderVisibility(string folderId)
    {
        var pref = GetOrCreatePref(folderId);
        pref.Hidden = !pref.Hidden;
        Save();
    }

    public void MoveFolder(string folderId, int direction, IEnumerable<MailFolderItem> currentList)
    {
        var list = currentList.ToList();
        var index = list.FindIndex(f => f.Id.Equals(folderId, StringComparison.OrdinalIgnoreCase));
        if (index == -1) return;

        var targetIndex = index + direction;
        if (targetIndex < 0 || targetIndex >= list.Count) return;

        // Reassign orders based on target position
        var item = list[index];
        list.RemoveAt(index);
        list.Insert(targetIndex, item);

        for (int i = 0; i < list.Count; i++)
        {
            var p = GetOrCreatePref(list[i].Id);
            p.Order = i;
        }

        Save();
    }

    public void ResetFolder(string folderId)
    {
        if (_preferences.ContainsKey(folderId))
        {
            _preferences.Remove(folderId);
            Save();
        }
    }

    public List<MailFolderItem> BuildFolderTree(IEnumerable<MailFolderDto> rawFolders, bool showHidden = false)
    {
        var folderList = rawFolders.ToList();
        if (folderList.Count == 0) return new List<MailFolderItem>();

        var map = new Dictionary<string, (MailFolderItem Item, List<string> ChildIds)>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in folderList)
        {
            _preferences.TryGetValue(f.Id, out var pref);

            var item = new MailFolderItem
            {
                Id = f.Id,
                DisplayName = f.DisplayName,
                CustomDisplayName = pref?.CustomName,
                TotalItemCount = f.TotalItemCount ?? 0,
                UnreadItemCount = f.UnreadItemCount ?? 0,
                ParentFolderId = f.ParentFolderId,
                Order = pref?.Order ?? 999,
                IsHidden = pref?.Hidden ?? false,
                CustomIcon = pref?.CustomIcon,
                Color = pref?.Color
            };

            map[f.Id] = (item, new List<string>());
        }

        var roots = new List<MailFolderItem>();
        foreach (var f in folderList)
        {
            var node = map[f.Id].Item;
            if (!string.IsNullOrEmpty(f.ParentFolderId) && map.ContainsKey(f.ParentFolderId))
            {
                map[f.ParentFolderId].ChildIds.Add(f.Id);
            }
            else
            {
                roots.Add(node);
            }
        }

        var standardOrder = new[] { "inbox", "archive", "sentitems", "drafts", "deleteditems", "junkemail" };

        int CompareFolders(MailFolderItem a, MailFolderItem b)
        {
            if (a.Order != 999 || b.Order != 999)
            {
                return a.Order.CompareTo(b.Order);
            }

            var nameA = a.DisplayName.ToLowerInvariant().Replace(" ", "");
            var nameB = b.DisplayName.ToLowerInvariant().Replace(" ", "");

            int idxA = Array.IndexOf(standardOrder, nameA);
            int idxB = Array.IndexOf(standardOrder, nameB);

            if (idxA != -1 && idxB != -1) return idxA.CompareTo(idxB);
            if (idxA != -1) return -1;
            if (idxB != -1) return 1;

            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        }

        var result = new List<MailFolderItem>();

        void ProcessLevel(List<MailFolderItem> nodes, int level)
        {
            var sorted = nodes.OrderBy(n => n, Comparer<MailFolderItem>.Create(CompareFolders)).ToList();
            foreach (var node in sorted)
            {
                node.Level = level;

                if (!node.IsHidden || showHidden)
                {
                    result.Add(node);
                }

                if (map.TryGetValue(node.Id, out var tuple) && tuple.ChildIds.Count > 0)
                {
                    var children = tuple.ChildIds.Where(id => map.ContainsKey(id)).Select(id => map[id].Item).ToList();
                    ProcessLevel(children, level + 1);
                }
            }
        }

        ProcessLevel(roots, 0);
        return result;
    }
}
