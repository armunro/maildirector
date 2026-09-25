using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailDirector.Models;

namespace MailDirector.Services;

public interface IRuleService
{
    List<FilterRule> GetAllRules();
    Task SaveRuleAsync(FilterRule rule);
    Task DeleteRuleAsync(string ruleName);
    bool Matches(FilterRule rule, EmailDto email);
    Task<FilterRule> CreateRuleFromEmailAsync(string messageId, string? ruleName = null);
    Task<FilterRule> AddSenderToRuleAsync(string ruleName, string senderEmail, string subject);
    Task ApplyRuleAsync(string messageId, string ruleName);
    Task ApplyRuleToFolderAsync(string folderId, string ruleName, IProgress<RuleProgressReport>? progress = null, CancellationToken cancellationToken = default);
    event EventHandler? RulesChanged;
}
