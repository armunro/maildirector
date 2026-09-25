using System.Collections.Generic;

namespace MailDirector.Models;

public class FilterRule
{
    public string Name { get; set; } = string.Empty;
    public string? OriginalName { get; set; }
    public string? Color { get; set; }
    public string? RootFolder { get; set; }
    public List<FilterCriteria> Filters { get; set; } = new();
    public List<RuleAction> Actions { get; set; } = new();
}

public class FilterCriteria
{
    public List<string>? From { get; set; }
    public List<string>? SubjectContains { get; set; }
    public List<string>? BodyContains { get; set; }
    public bool? HasAttachments { get; set; }
}

public class RuleAction
{
    public ActionType Type { get; set; }
    public string? Value { get; set; }
}

public enum ActionType
{
    Star,
    AddCategory,
    Archive,
    Move,
    MarkAsRead,
    ClearFlag
}
