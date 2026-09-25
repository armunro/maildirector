using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MailDirector.Models;

public record EmailDto(
    string Id,
    string From,
    string FromAddress,
    string Subject,
    string BodyPreview,
    DateTimeOffset ReceivedDateTime,
    string WebLink,
    string? Body = null,
    List<string>? MatchingRules = null,
    bool IsRead = false,
    bool IsFlagged = false,
    List<string>? Categories = null);

public record MailFolderDto(
    string Id,
    string DisplayName,
    int? TotalItemCount = 0,
    int? UnreadItemCount = 0,
    string? ParentFolderId = null,
    int? ChildFolderCount = 0);

public record MatchingRuleDto(string Name, string? Color);

public class FolderPreference
{
    public int Order { get; set; } = 999;
    public bool Hidden { get; set; } = false;
    public string? CustomName { get; set; }
    public string? CustomIcon { get; set; }
    public string? Color { get; set; }
}

public class RuleProgressReport
{
    public int Current { get; set; }
    public int Total { get; set; }
    public string? Subject { get; set; }
    public bool IsCompleted { get; set; }
    public string? ErrorMessage { get; set; }
}

public class EmailItem : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _from = string.Empty;
    private string _fromAddress = string.Empty;
    private string _subject = string.Empty;
    private string _bodyPreview = string.Empty;
    private string? _body;
    private DateTimeOffset _receivedDateTime;
    private string _webLink = string.Empty;
    private bool _isRead;
    private bool _isFlagged;
    private ObservableCollection<MatchingRuleDto> _matchingRules = new();

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string From
    {
        get => _from;
        set => SetProperty(ref _from, value);
    }

    public string FromAddress
    {
        get => _fromAddress;
        set => SetProperty(ref _fromAddress, value);
    }

    public string Subject
    {
        get => _subject;
        set => SetProperty(ref _subject, value);
    }

    public string BodyPreview
    {
        get => _bodyPreview;
        set => SetProperty(ref _bodyPreview, value);
    }

    public string? Body
    {
        get => _body;
        set => SetProperty(ref _body, value);
    }

    public DateTimeOffset ReceivedDateTime
    {
        get => _receivedDateTime;
        set => SetProperty(ref _receivedDateTime, value);
    }

    public string WebLink
    {
        get => _webLink;
        set => SetProperty(ref _webLink, value);
    }

    public bool IsRead
    {
        get => _isRead;
        set => SetProperty(ref _isRead, value);
    }

    public bool IsFlagged
    {
        get => _isFlagged;
        set => SetProperty(ref _isFlagged, value);
    }

    public ObservableCollection<MatchingRuleDto> MatchingRules
    {
        get => _matchingRules;
        set => SetProperty(ref _matchingRules, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    public static EmailItem FromDto(EmailDto dto, IEnumerable<MatchingRuleDto>? matchingRules = null)
    {
        var item = new EmailItem
        {
            Id = dto.Id,
            From = dto.From,
            FromAddress = dto.FromAddress,
            Subject = dto.Subject,
            BodyPreview = dto.BodyPreview,
            Body = dto.Body,
            ReceivedDateTime = dto.ReceivedDateTime,
            WebLink = dto.WebLink,
            IsRead = dto.IsRead,
            IsFlagged = dto.IsFlagged
        };

        if (matchingRules != null)
        {
            foreach (var r in matchingRules)
            {
                item.MatchingRules.Add(r);
            }
        }

        return item;
    }
}

public class MailFolderItem : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _displayName = string.Empty;
    private string? _customDisplayName;
    private int _unreadItemCount;
    private int _totalItemCount;
    private string? _parentFolderId;
    private int _level;
    private int _order = 999;
    private bool _isHidden;
    private string? _customIcon;
    private string? _color;
    private bool _isEditing;

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (SetProperty(ref _displayName, value))
            {
                OnPropertyChanged(nameof(EffectiveDisplayName));
            }
        }
    }

    public string? CustomDisplayName
    {
        get => _customDisplayName;
        set
        {
            if (SetProperty(ref _customDisplayName, value))
            {
                OnPropertyChanged(nameof(EffectiveDisplayName));
            }
        }
    }

    public string EffectiveDisplayName => !string.IsNullOrWhiteSpace(CustomDisplayName) ? CustomDisplayName : DisplayName;

    public int UnreadItemCount
    {
        get => _unreadItemCount;
        set => SetProperty(ref _unreadItemCount, value);
    }

    public int TotalItemCount
    {
        get => _totalItemCount;
        set => SetProperty(ref _totalItemCount, value);
    }

    public string? ParentFolderId
    {
        get => _parentFolderId;
        set => SetProperty(ref _parentFolderId, value);
    }

    public int Level
    {
        get => _level;
        set => SetProperty(ref _level, value);
    }

    public int Order
    {
        get => _order;
        set => SetProperty(ref _order, value);
    }

    public bool IsHidden
    {
        get => _isHidden;
        set => SetProperty(ref _isHidden, value);
    }

    public string? CustomIcon
    {
        get => _customIcon;
        set => SetProperty(ref _customIcon, value);
    }

    public string? Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
