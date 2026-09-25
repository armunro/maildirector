using System.Collections.Generic;
using MailDirector.Models;

namespace MailDirector.Services;

public interface IFolderPreferenceService
{
    Dictionary<string, FolderPreference> Preferences { get; }
    void Load();
    void Save();
    List<MailFolderItem> BuildFolderTree(IEnumerable<MailFolderDto> rawFolders, bool showHidden = false);
    void UpdateFolderCustomName(string folderId, string? customName);
    void UpdateFolderIcon(string folderId, string? icon);
    void UpdateFolderColor(string folderId, string? color);
    void ToggleFolderVisibility(string folderId);
    void MoveFolder(string folderId, int direction, IEnumerable<MailFolderItem> currentList);
    void ResetFolder(string folderId);
}
