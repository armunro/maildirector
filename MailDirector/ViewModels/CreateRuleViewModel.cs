using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MailDirector.Models;
using MailDirector.Services;

namespace MailDirector.ViewModels;

public class CreateRuleViewModel : BaseViewModel
{
    private readonly IRuleService _ruleService;
    private readonly EmailItem _email;
    private bool _isCreatingNew = true;
    private string _ruleName = string.Empty;
    private string _ruleColor = "#3B82F6";
    private string _targetFolder = "Archive";
    private bool _markAsRead = true;
    private FilterRule? _selectedExistingRule;
    private ObservableCollection<FilterRule> _existingRules = new();

    public bool IsCreatingNew
    {
        get => _isCreatingNew;
        set => SetProperty(ref _isCreatingNew, value);
    }

    public string RuleName
    {
        get => _ruleName;
        set => SetProperty(ref _ruleName, value);
    }

    public string RuleColor
    {
        get => _ruleColor;
        set => SetProperty(ref _ruleColor, value);
    }

    public string TargetFolder
    {
        get => _targetFolder;
        set => SetProperty(ref _targetFolder, value);
    }

    public bool MarkAsRead
    {
        get => _markAsRead;
        set => SetProperty(ref _markAsRead, value);
    }

    public string SenderEmail => _email.FromAddress;
    public string SenderName => _email.From;
    public string EmailSubject => _email.Subject;

    public FilterRule? SelectedExistingRule
    {
        get => _selectedExistingRule;
        set => SetProperty(ref _selectedExistingRule, value);
    }

    public ObservableCollection<FilterRule> ExistingRules
    {
        get => _existingRules;
        set => SetProperty(ref _existingRules, value);
    }

    public ICommand SaveCommand { get; }

    public event EventHandler<bool>? RequestClose;

    public CreateRuleViewModel(IRuleService ruleService, EmailItem email, bool addToExisting = false)
    {
        _ruleService = ruleService;
        _email = email;
        _isCreatingNew = !addToExisting;

        _ruleName = !string.IsNullOrWhiteSpace(email.From) ? email.From : (!string.IsNullOrWhiteSpace(email.FromAddress) ? email.FromAddress : "New Rule");

        var all = _ruleService.GetAllRules();
        ExistingRules = new ObservableCollection<FilterRule>(all);
        if (ExistingRules.Count > 0)
        {
            SelectedExistingRule = ExistingRules[0];
        }

        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    private async Task SaveAsync()
    {
        if (IsCreatingNew)
        {
            if (string.IsNullOrWhiteSpace(RuleName)) return;

            var actions = new List<RuleAction>();
            if (MarkAsRead) actions.Add(new RuleAction { Type = ActionType.MarkAsRead });
            if (!string.IsNullOrWhiteSpace(TargetFolder)) actions.Add(new RuleAction { Type = ActionType.Move, Value = TargetFolder.Trim() });

            var rule = new FilterRule
            {
                Name = RuleName.Trim(),
                Color = RuleColor,
                RootFolder = "Inbox",
                Filters = new List<FilterCriteria>
                {
                    new FilterCriteria
                    {
                        From = new List<string> { !string.IsNullOrWhiteSpace(SenderEmail) ? SenderEmail : SenderName }
                    }
                },
                Actions = actions
            };

            await _ruleService.SaveRuleAsync(rule);
        }
        else
        {
            if (SelectedExistingRule == null) return;
            await _ruleService.AddSenderToRuleAsync(SelectedExistingRule.Name, !string.IsNullOrWhiteSpace(SenderEmail) ? SenderEmail : SenderName, string.Empty);
        }

        RequestClose?.Invoke(this, true);
    }
}
