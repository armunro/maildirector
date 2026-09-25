using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MailDirector.Models;
using MailDirector.Services;

namespace MailDirector.ViewModels;

public class FilterCriteriaViewModel : BaseViewModel
{
    private string _fromString = string.Empty;
    private string _subjectContainsString = string.Empty;
    private string _bodyContainsString = string.Empty;
    private bool? _hasAttachments;

    public string FromString
    {
        get => _fromString;
        set => SetProperty(ref _fromString, value);
    }

    public string SubjectContainsString
    {
        get => _subjectContainsString;
        set => SetProperty(ref _subjectContainsString, value);
    }

    public string BodyContainsString
    {
        get => _bodyContainsString;
        set => SetProperty(ref _bodyContainsString, value);
    }

    public bool? HasAttachments
    {
        get => _hasAttachments;
        set => SetProperty(ref _hasAttachments, value);
    }

    public static FilterCriteriaViewModel FromModel(FilterCriteria criteria)
    {
        return new FilterCriteriaViewModel
        {
            FromString = criteria.From != null ? string.Join(", ", criteria.From) : string.Empty,
            SubjectContainsString = criteria.SubjectContains != null ? string.Join(", ", criteria.SubjectContains) : string.Empty,
            BodyContainsString = criteria.BodyContains != null ? string.Join(", ", criteria.BodyContains) : string.Empty,
            HasAttachments = criteria.HasAttachments
        };
    }

    public FilterCriteria ToModel()
    {
        return new FilterCriteria
        {
            From = !string.IsNullOrWhiteSpace(FromString)
                ? FromString.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList()
                : null,
            SubjectContains = !string.IsNullOrWhiteSpace(SubjectContainsString)
                ? SubjectContainsString.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList()
                : null,
            BodyContains = !string.IsNullOrWhiteSpace(BodyContainsString)
                ? BodyContainsString.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList()
                : null,
            HasAttachments = HasAttachments
        };
    }
}

public class RuleActionViewModel : BaseViewModel
{
    private ActionType _type;
    private string? _value;

    public ActionType Type
    {
        get => _type;
        set
        {
            if (SetProperty(ref _type, value))
            {
                OnPropertyChanged(nameof(RequiresValue));
                OnPropertyChanged(nameof(ValuePlaceholder));
            }
        }
    }

    public string? Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public bool RequiresValue => Type == ActionType.Move || Type == ActionType.AddCategory;

    public string ValuePlaceholder => Type switch
    {
        ActionType.Move => "Target Folder (e.g. Archive, Projects\\FlightDeck)",
        ActionType.AddCategory => "Category Name (e.g. Work, Jira)",
        _ => string.Empty
    };

    public static Array AvailableActionTypes => Enum.GetValues(typeof(ActionType));

    public static RuleActionViewModel FromModel(RuleAction action)
    {
        return new RuleActionViewModel
        {
            Type = action.Type,
            Value = action.Value
        };
    }

    public RuleAction ToModel()
    {
        return new RuleAction
        {
            Type = Type,
            Value = RequiresValue ? Value?.Trim() : null
        };
    }
}

public class RulesManagerViewModel : BaseViewModel
{
    private readonly IRuleService _ruleService;
    private ObservableCollection<FilterRule> _rules = new();
    private FilterRule? _selectedRule;

    private string _name = string.Empty;
    private string? _originalName;
    private string _color = "#0052CC";
    private string _rootFolder = "Inbox";
    private ObservableCollection<FilterCriteriaViewModel> _filters = new();
    private ObservableCollection<RuleActionViewModel> _actions = new();
    private string? _statusMessage;
    private bool _isEditing;

    public ObservableCollection<FilterRule> Rules
    {
        get => _rules;
        set => SetProperty(ref _rules, value);
    }

    public FilterRule? SelectedRule
    {
        get => _selectedRule;
        set
        {
            if (SetProperty(ref _selectedRule, value))
            {
                LoadRuleForEditing(value);
            }
        }
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }

    public string RootFolder
    {
        get => _rootFolder;
        set => SetProperty(ref _rootFolder, value);
    }

    public ObservableCollection<FilterCriteriaViewModel> Filters
    {
        get => _filters;
        set => SetProperty(ref _filters, value);
    }

    public ObservableCollection<RuleActionViewModel> Actions
    {
        get => _actions;
        set => SetProperty(ref _actions, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public ICommand NewRuleCommand { get; }
    public ICommand SaveRuleCommand { get; }
    public ICommand DeleteRuleCommand { get; }
    public ICommand AddFilterCommand { get; }
    public ICommand RemoveFilterCommand { get; }
    public ICommand AddActionCommand { get; }
    public ICommand RemoveActionCommand { get; }
    public ICommand DuplicateRuleCommand { get; }

    public event EventHandler? RequestClose;

    public RulesManagerViewModel(IRuleService ruleService)
    {
        _ruleService = ruleService;

        NewRuleCommand = new RelayCommand(NewRule);
        SaveRuleCommand = new AsyncRelayCommand(SaveRuleAsync, () => !string.IsNullOrWhiteSpace(Name));
        DeleteRuleCommand = new AsyncRelayCommand(DeleteRuleAsync, () => SelectedRule != null);
        AddFilterCommand = new RelayCommand(AddFilter);
        RemoveFilterCommand = new RelayCommand<FilterCriteriaViewModel>(RemoveFilter);
        AddActionCommand = new RelayCommand(AddAction);
        RemoveActionCommand = new RelayCommand<RuleActionViewModel>(RemoveAction);
        DuplicateRuleCommand = new RelayCommand(DuplicateRule, () => SelectedRule != null);

        LoadAllRules();
    }

    public void LoadAllRules()
    {
        var all = _ruleService.GetAllRules();
        Rules = new ObservableCollection<FilterRule>(all);

        if (Rules.Count > 0)
        {
            SelectedRule = Rules[0];
        }
        else
        {
            NewRule();
        }
    }

    private void LoadRuleForEditing(FilterRule? rule)
    {
        if (rule == null)
        {
            IsEditing = false;
            return;
        }

        IsEditing = true;
        _originalName = rule.Name;
        Name = rule.Name;
        Color = !string.IsNullOrEmpty(rule.Color) ? rule.Color : "#0052CC";
        RootFolder = !string.IsNullOrEmpty(rule.RootFolder) ? rule.RootFolder : "Inbox";

        Filters = new ObservableCollection<FilterCriteriaViewModel>(
            (rule.Filters ?? new List<FilterCriteria>()).Select(FilterCriteriaViewModel.FromModel));

        Actions = new ObservableCollection<RuleActionViewModel>(
            (rule.Actions ?? new List<RuleAction>()).Select(RuleActionViewModel.FromModel));

        if (Filters.Count == 0)
        {
            Filters.Add(new FilterCriteriaViewModel());
        }

        if (Actions.Count == 0)
        {
            Actions.Add(new RuleActionViewModel { Type = ActionType.MarkAsRead });
        }
    }

    private void NewRule()
    {
        _selectedRule = null;
        OnPropertyChanged(nameof(SelectedRule));

        IsEditing = true;
        _originalName = null;
        Name = "New Rule";
        Color = "#3B82F6";
        RootFolder = "Inbox";

        Filters = new ObservableCollection<FilterCriteriaViewModel>
        {
            new FilterCriteriaViewModel()
        };

        Actions = new ObservableCollection<RuleActionViewModel>
        {
            new RuleActionViewModel { Type = ActionType.MarkAsRead }
        };
    }

    private void DuplicateRule()
    {
        if (SelectedRule == null) return;

        var dupName = $"{Name} (Copy)";
        _originalName = null;
        Name = dupName;
    }

    private void AddFilter()
    {
        Filters.Add(new FilterCriteriaViewModel());
    }

    private void RemoveFilter(FilterCriteriaViewModel? filter)
    {
        if (filter != null && Filters.Count > 1)
        {
            Filters.Remove(filter);
        }
    }

    private void AddAction()
    {
        Actions.Add(new RuleActionViewModel { Type = ActionType.MarkAsRead });
    }

    private void RemoveAction(RuleActionViewModel? action)
    {
        if (action != null && Actions.Count > 1)
        {
            Actions.Remove(action);
        }
    }

    private async Task SaveRuleAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) return;

        var rule = new FilterRule
        {
            Name = Name.Trim(),
            OriginalName = _originalName,
            Color = Color,
            RootFolder = RootFolder,
            Filters = Filters.Select(f => f.ToModel()).ToList(),
            Actions = Actions.Select(a => a.ToModel()).ToList()
        };

        await _ruleService.SaveRuleAsync(rule);
        _originalName = rule.Name;

        LoadAllRules();
        SelectedRule = Rules.FirstOrDefault(r => r.Name.Equals(rule.Name, StringComparison.OrdinalIgnoreCase));
        StatusMessage = $"Rule '{rule.Name}' saved successfully.";
    }

    private async Task DeleteRuleAsync()
    {
        if (SelectedRule == null) return;

        var nameToDelete = SelectedRule.Name;
        await _ruleService.DeleteRuleAsync(nameToDelete);

        LoadAllRules();
        StatusMessage = $"Rule '{nameToDelete}' deleted.";
    }
}
