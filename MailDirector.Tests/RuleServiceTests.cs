using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MailDirector.Models;
using MailDirector.Services;
using Xunit;

namespace MailDirector.Tests;

public class RuleServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly StorageService _storageService;
    private readonly MockEmailService _emailService;
    private readonly RuleService _ruleService;

    public RuleServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FlightMail_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        _storageService = new StorageService(_testDir);
        _emailService = new MockEmailService();
        _ruleService = new RuleService(_emailService, _storageService);
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
    public void Matches_SenderFilter_MatchesCorrectly()
    {
        var rule = new FilterRule
        {
            Name = "Jira Rule",
            Filters = new List<FilterCriteria>
            {
                new FilterCriteria { From = new List<string> { "jira@atlassian.net", "jira@" } }
            }
        };

        var matchingEmail = new EmailDto("1", "Jira Notifications", "jira@atlassian.net", "Issue updated", "Preview", DateTimeOffset.Now, "link");
        var nonMatchingEmail = new EmailDto("2", "GitHub", "notifications@github.com", "PR merged", "Preview", DateTimeOffset.Now, "link");

        Assert.True(_ruleService.Matches(rule, matchingEmail));
        Assert.False(_ruleService.Matches(rule, nonMatchingEmail));
    }

    [Fact]
    public void Matches_SubjectFilter_MatchesCorrectly()
    {
        var rule = new FilterRule
        {
            Name = "Security Rule",
            Filters = new List<FilterCriteria>
            {
                new FilterCriteria { SubjectContains = new List<string> { "[Security Alert]", "Urgent" } }
            }
        };

        var email1 = new EmailDto("1", "Sec", "sec@corp.com", "[Security Alert] Account locked", "Preview", DateTimeOffset.Now, "link");
        var email2 = new EmailDto("2", "Sec", "sec@corp.com", "Urgent: review needed", "Preview", DateTimeOffset.Now, "link");
        var email3 = new EmailDto("3", "Sec", "sec@corp.com", "Weekly Newsletter", "Preview", DateTimeOffset.Now, "link");

        Assert.True(_ruleService.Matches(rule, email1));
        Assert.True(_ruleService.Matches(rule, email2));
        Assert.False(_ruleService.Matches(rule, email3));
    }

    [Fact]
    public void Matches_BodyFilter_MatchesCorrectly()
    {
        var rule = new FilterRule
        {
            Name = "Invoice Rule",
            Filters = new List<FilterCriteria>
            {
                new FilterCriteria { BodyContains = new List<string> { "invoice", "payment due" } }
            }
        };

        var email1 = new EmailDto("1", "Billing", "bill@corp.com", "Receipt", "Your invoice is ready for download", DateTimeOffset.Now, "link");
        var email2 = new EmailDto("2", "Billing", "bill@corp.com", "Receipt", "General message", DateTimeOffset.Now, "link");

        Assert.True(_ruleService.Matches(rule, email1));
        Assert.False(_ruleService.Matches(rule, email2));
    }

    [Fact]
    public async Task SaveRuleAsync_And_DeleteRuleAsync_PersistsAndRemoves()
    {
        var rule = new FilterRule
        {
            Name = "Custom Test Rule",
            Color = "#123456",
            RootFolder = "Inbox",
            Filters = new List<FilterCriteria>
            {
                new FilterCriteria { From = new List<string> { "custom@test.com" } }
            },
            Actions = new List<RuleAction>
            {
                new RuleAction { Type = ActionType.MarkAsRead },
                new RuleAction { Type = ActionType.AddCategory, Value = "Custom" }
            }
        };

        await _ruleService.SaveRuleAsync(rule);

        var allRules = _ruleService.GetAllRules();
        Assert.Contains(allRules, r => r.Name == "Custom Test Rule");

        // Verify file was written to disk
        var files = Directory.GetFiles(_storageService.GetRulesDirectory(), "*.yaml");
        Assert.Contains(files, f => f.Contains("Custom-Test-Rule"));

        // Delete rule
        await _ruleService.DeleteRuleAsync("Custom Test Rule");
        allRules = _ruleService.GetAllRules();
        Assert.DoesNotContain(allRules, r => r.Name == "Custom Test Rule");
    }

    [Fact]
    public async Task SaveRuleAsync_RenamingRule_DeletesOldFile()
    {
        var rule = new FilterRule
        {
            Name = "Initial Rule Name",
            Filters = new List<FilterCriteria>
            {
                new FilterCriteria { From = new List<string> { "test@test.com" } }
            }
        };

        await _ruleService.SaveRuleAsync(rule);

        var oldFile = Path.Combine(_storageService.GetRulesDirectory(), "Initial-Rule-Name.yaml");
        Assert.True(File.Exists(oldFile));

        // Rename rule
        rule.OriginalName = "Initial Rule Name";
        rule.Name = "New Renamed Rule";
        await _ruleService.SaveRuleAsync(rule);

        Assert.False(File.Exists(oldFile));
        var newFile = Path.Combine(_storageService.GetRulesDirectory(), "New-Renamed-Rule.yaml");
        Assert.True(File.Exists(newFile));

        var rules = _ruleService.GetAllRules();
        Assert.Contains(rules, r => r.Name == "New Renamed Rule");
        Assert.DoesNotContain(rules, r => r.Name == "Initial Rule Name");
    }

    [Fact]
    public async Task CreateRuleFromEmailAsync_CreatesRuleForSender()
    {
        var email = new EmailDto("test-msg", "John Doe", "john.doe@example.com", "Meeting Notice", "Let's meet tomorrow", DateTimeOffset.Now, "link");
        _emailService.AddEmail(email);

        var rule = await _ruleService.CreateRuleFromEmailAsync("test-msg", "John Doe Rule");

        Assert.NotNull(rule);
        Assert.Equal("John Doe Rule", rule.Name);
        Assert.Contains("john.doe@example.com", rule.Filters[0].From!);
        Assert.Contains(rule.Actions, a => a.Type == ActionType.MarkAsRead);
    }

    [Fact]
    public async Task AddSenderToRuleAsync_AddsSenderToExistingRule()
    {
        var rule = new FilterRule
        {
            Name = "Multi Sender Rule",
            Filters = new List<FilterCriteria>
            {
                new FilterCriteria { From = new List<string> { "first@example.com" } }
            }
        };

        await _ruleService.SaveRuleAsync(rule);

        var updated = await _ruleService.AddSenderToRuleAsync("Multi Sender Rule", "second@example.com", "Alert Subject");

        Assert.Equal(2, updated.Filters.Count);
        Assert.Contains(updated.Filters, f => f.From != null && f.From.Contains("second@example.com"));
    }

    [Fact]
    public async Task ApplyRuleToFolderAsync_AppliesActionsWithProgress()
    {
        var email1 = new EmailDto("apply-1", "Match", "match@corp.com", "Test 1", "Preview", DateTimeOffset.Now, "link", IsRead: false);
        var email2 = new EmailDto("apply-2", "NoMatch", "other@corp.com", "Test 2", "Preview", DateTimeOffset.Now, "link", IsRead: false);

        _emailService.AddEmail(email1, "inbox");
        _emailService.AddEmail(email2, "inbox");

        var rule = new FilterRule
        {
            Name = "Auto Read Match",
            Filters = new List<FilterCriteria>
            {
                new FilterCriteria { From = new List<string> { "match@corp.com" } }
            },
            Actions = new List<RuleAction>
            {
                new RuleAction { Type = ActionType.MarkAsRead },
                new RuleAction { Type = ActionType.Star }
            }
        };

        await _ruleService.SaveRuleAsync(rule);

        var progressReports = new List<RuleProgressReport>();
        var progress = new Progress<RuleProgressReport>(r => progressReports.Add(r));

        await _ruleService.ApplyRuleToFolderAsync("inbox", "Auto Read Match", progress);

        var updatedEmail1 = await _emailService.GetEmailAsync("apply-1");
        Assert.NotNull(updatedEmail1);
        Assert.True(updatedEmail1.IsRead);
        Assert.True(updatedEmail1.IsFlagged);
    }
}
