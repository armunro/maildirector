using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailDirector.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MailDirector.Services;

public class RuleService : IRuleService
{
    private readonly IEmailService _emailService;
    private readonly IStorageService _storageService;
    private readonly List<FilterRule> _rules = new();
    private readonly string _rulesDirectory;
    private readonly object _lock = new();

    public event EventHandler? RulesChanged;

    public RuleService(IEmailService emailService, IStorageService storageService)
    {
        _emailService = emailService;
        _storageService = storageService;
        _rulesDirectory = _storageService.GetRulesDirectory();

        MigrateRules();
        LoadRules();
    }

    private void MigrateRules()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var flightPlanRulesDir = Path.Combine(appData, "FlightPlan", "Rules");
            if (Directory.Exists(flightPlanRulesDir) && !string.Equals(flightPlanRulesDir, _rulesDirectory, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var file in Directory.GetFiles(flightPlanRulesDir, "*.yaml"))
                {
                    var destFile = Path.Combine(_rulesDirectory, Path.GetFileName(file));
                    if (!File.Exists(destFile))
                    {
                        try { File.Copy(file, destFile); } catch { }
                    }
                }
            }
        }
        catch { }
    }

    public void LoadRules()
    {
        lock (_lock)
        {
            _rules.Clear();

            if (!Directory.Exists(_rulesDirectory))
            {
                SeedDefaultRules();
                return;
            }

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var files = Directory.GetFiles(_rulesDirectory, "*.yaml");
            if (files.Length == 0)
            {
                SeedDefaultRules();
                return;
            }

            foreach (var file in files)
            {
                try
                {
                    var yaml = File.ReadAllText(file);
                    var rule = deserializer.Deserialize<FilterRule>(yaml);
                    if (rule != null)
                    {
                        if (string.IsNullOrEmpty(rule.Name))
                        {
                            rule.Name = Path.GetFileNameWithoutExtension(file);
                        }
                        _rules.Add(rule);
                    }
                }
                catch
                {
                    // Ignore unparseable rule file
                }
            }
        }

        RulesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SeedDefaultRules()
    {
        var defaultRules = new List<FilterRule>
        {
            new FilterRule
            {
                Name = "Jira Notifications",
                Color = "#0052CC",
                RootFolder = "Inbox",
                Filters = new List<FilterCriteria>
                {
                    new FilterCriteria
                    {
                        From = new List<string> { "jira@", "atlassian.net" },
                        SubjectContains = new List<string> { "[JIRA]" }
                    }
                },
                Actions = new List<RuleAction>
                {
                    new RuleAction { Type = ActionType.AddCategory, Value = "Jira" },
                    new RuleAction { Type = ActionType.Star }
                }
            },
            new FilterRule
            {
                Name = "GitHub Activity",
                Color = "#2EA44F",
                RootFolder = "Inbox",
                Filters = new List<FilterCriteria>
                {
                    new FilterCriteria
                    {
                        From = new List<string> { "github.com", "notifications@github.com" },
                        SubjectContains = new List<string> { "[GitHub]" }
                    }
                },
                Actions = new List<RuleAction>
                {
                    new RuleAction { Type = ActionType.AddCategory, Value = "Dev" }
                }
            },
            new FilterRule
            {
                Name = "Security Reports",
                Color = "#D9534F",
                RootFolder = "Inbox",
                Filters = new List<FilterCriteria>
                {
                    new FilterCriteria
                    {
                        From = new List<string> { "alerts@azure.com", "security@" },
                        SubjectContains = new List<string> { "Security", "Alert" }
                    }
                },
                Actions = new List<RuleAction>
                {
                    new RuleAction { Type = ActionType.AddCategory, Value = "Security" }
                }
            },
            new FilterRule
            {
                Name = "TeamCity Builds",
                Color = "#F97316",
                RootFolder = "Inbox",
                Filters = new List<FilterCriteria>
                {
                    new FilterCriteria
                    {
                        From = new List<string> { "teamcity@" },
                        SubjectContains = new List<string> { "[TeamCity]" }
                    }
                },
                Actions = new List<RuleAction>
                {
                    new RuleAction { Type = ActionType.AddCategory, Value = "CI/CD" }
                }
            }
        };

        foreach (var rule in defaultRules)
        {
            _ = SaveRuleAsync(rule);
        }
    }

    public List<FilterRule> GetAllRules()
    {
        lock (_lock)
        {
            return _rules.ToList();
        }
    }

    public async Task SaveRuleAsync(FilterRule rule)
    {
        lock (_lock)
        {
            if (!string.IsNullOrEmpty(rule.OriginalName) && !rule.OriginalName.Equals(rule.Name, StringComparison.OrdinalIgnoreCase))
            {
                var oldFileName = GetSafeFileName(rule.OriginalName);
                var oldFilePath = Path.Combine(_rulesDirectory, oldFileName);
                if (File.Exists(oldFilePath))
                {
                    try { File.Delete(oldFilePath); } catch { }
                }

                var oldRule = _rules.FirstOrDefault(r => r.Name.Equals(rule.OriginalName, StringComparison.OrdinalIgnoreCase));
                if (oldRule != null)
                {
                    _rules.Remove(oldRule);
                }
            }
        }

        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var originalNameBackup = rule.OriginalName;
        rule.OriginalName = null;
        var yaml = serializer.Serialize(rule);
        rule.OriginalName = originalNameBackup;

        var fileName = GetSafeFileName(rule.Name);
        var filePath = Path.Combine(_rulesDirectory, fileName);

        if (!Directory.Exists(_rulesDirectory))
        {
            Directory.CreateDirectory(_rulesDirectory);
        }

        await File.WriteAllTextAsync(filePath, yaml);

        lock (_lock)
        {
            var existing = _rules.FirstOrDefault(r => r.Name.Equals(rule.Name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                _rules.Remove(existing);
            }
            _rules.Add(rule);
        }

        RulesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteRuleAsync(string ruleName)
    {
        FilterRule? ruleToDelete = null;
        lock (_lock)
        {
            ruleToDelete = _rules.FirstOrDefault(r => r.Name.Equals(ruleName, StringComparison.OrdinalIgnoreCase));
            if (ruleToDelete != null)
            {
                _rules.Remove(ruleToDelete);
            }
        }

        if (ruleToDelete != null)
        {
            var fileName = GetSafeFileName(ruleName);
            var filePath = Path.Combine(_rulesDirectory, fileName);
            if (File.Exists(filePath))
            {
                try { File.Delete(filePath); } catch { }
            }

            RulesChanged?.Invoke(this, EventArgs.Empty);
        }

        await Task.CompletedTask;
    }

    public bool Matches(FilterRule rule, EmailDto email)
    {
        if (rule.Filters == null || !rule.Filters.Any())
            return true;

        return rule.Filters.Any(criteria => MatchesCriteria(criteria, email));
    }

    private bool MatchesCriteria(FilterCriteria criteria, EmailDto email)
    {
        // From filter
        if (criteria.From != null && criteria.From.Any())
        {
            bool matchesFrom = criteria.From.Any(f =>
                (!string.IsNullOrEmpty(email.From) && email.From.Contains(f, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(email.FromAddress) && email.FromAddress.Contains(f, StringComparison.OrdinalIgnoreCase)));

            if (!matchesFrom) return false;
        }

        // Subject filter
        if (criteria.SubjectContains != null && criteria.SubjectContains.Any())
        {
            bool matchesSubject = criteria.SubjectContains.Any(s =>
                !string.IsNullOrEmpty(email.Subject) && email.Subject.Contains(s, StringComparison.OrdinalIgnoreCase));

            if (!matchesSubject) return false;
        }

        // Body filter
        if (criteria.BodyContains != null && criteria.BodyContains.Any())
        {
            bool matchesBody = criteria.BodyContains.Any(b =>
                (!string.IsNullOrEmpty(email.BodyPreview) && email.BodyPreview.Contains(b, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(email.Body) && email.Body.Contains(b, StringComparison.OrdinalIgnoreCase)));

            if (!matchesBody) return false;
        }

        return true;
    }

    public async Task<FilterRule> CreateRuleFromEmailAsync(string messageId, string? ruleName = null)
    {
        var email = await _emailService.GetEmailAsync(messageId);
        if (email == null) throw new ArgumentException($"Email '{messageId}' not found.");

        if (string.IsNullOrWhiteSpace(ruleName))
        {
            ruleName = !string.IsNullOrWhiteSpace(email.From) ? email.From : "New Rule";
        }

        var rule = new FilterRule
        {
            Name = ruleName,
            Color = "#3498db",
            RootFolder = "Inbox",
            Filters = new List<FilterCriteria>
            {
                new FilterCriteria
                {
                    From = new List<string> { !string.IsNullOrWhiteSpace(email.FromAddress) ? email.FromAddress : email.From }
                }
            },
            Actions = new List<RuleAction>
            {
                new RuleAction { Type = ActionType.MarkAsRead },
                new RuleAction { Type = ActionType.Move, Value = "Archive" }
            }
        };

        await SaveRuleAsync(rule);
        return rule;
    }

    public async Task<FilterRule> AddSenderToRuleAsync(string ruleName, string senderEmail, string subject)
    {
        FilterRule? rule;
        lock (_lock)
        {
            rule = _rules.FirstOrDefault(r => r.Name.Equals(ruleName, StringComparison.OrdinalIgnoreCase));
        }

        if (rule == null) throw new ArgumentException($"Rule '{ruleName}' not found.");

        rule.Filters ??= new List<FilterCriteria>();
        rule.Filters.Add(new FilterCriteria
        {
            From = new List<string> { senderEmail },
            SubjectContains = string.IsNullOrEmpty(subject) ? new List<string>() : new List<string> { subject }
        });

        await SaveRuleAsync(rule);
        return rule;
    }

    public async Task ApplyRuleAsync(string messageId, string ruleName)
    {
        FilterRule? rule;
        lock (_lock)
        {
            rule = _rules.FirstOrDefault(r => r.Name.Equals(ruleName, StringComparison.OrdinalIgnoreCase));
        }

        if (rule == null) throw new ArgumentException($"Rule '{ruleName}' not found.");

        await _emailService.ApplyRuleActionsAsync(messageId, rule.Actions);
    }

    public async Task ApplyRuleToFolderAsync(string folderId, string ruleName, IProgress<RuleProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        FilterRule? rule;
        lock (_lock)
        {
            rule = _rules.FirstOrDefault(r => r.Name.Equals(ruleName, StringComparison.OrdinalIgnoreCase));
        }

        if (rule == null) throw new ArgumentException($"Rule '{ruleName}' not found.");

        var emails = (await _emailService.GetEmailsAsync(folderId, 100)).ToList();
        int total = emails.Count;
        int current = 0;

        foreach (var email in emails)
        {
            if (cancellationToken.IsCancellationRequested) break;

            current++;
            if (Matches(rule, email))
            {
                await _emailService.ApplyRuleActionsAsync(email.Id, rule.Actions);
            }

            progress?.Report(new RuleProgressReport
            {
                Current = current,
                Total = total,
                Subject = email.Subject,
                IsCompleted = current >= total
            });
        }
    }

    private static string GetSafeFileName(string ruleName)
    {
        var fileName = $"{ruleName.Replace(" ", "-")}.yaml";
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        return fileName;
    }
}
