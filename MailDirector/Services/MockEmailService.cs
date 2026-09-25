using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MailDirector.Models;

namespace MailDirector.Services;

public class MockEmailService : IEmailService
{
    private readonly object _lock = new();
    private readonly List<MailFolderDto> _mockFolders;
    private readonly List<EmailDto> _mockEmails;
    private readonly Dictionary<string, string> _emailFolderMap = new(StringComparer.OrdinalIgnoreCase);

    public MockEmailService()
    {
        _mockFolders = new List<MailFolderDto>
        {
            new MailFolderDto("inbox", "Inbox", 12, 4, null, 0),
            new MailFolderDto("archive", "Archive", 150, 0, null, 0),
            new MailFolderDto("sentitems", "Sent Items", 45, 0, null, 0),
            new MailFolderDto("drafts", "Drafts", 3, 3, null, 0),
            new MailFolderDto("deleteditems", "Deleted Items", 28, 0, null, 0),
            new MailFolderDto("junkemail", "Junk Email", 8, 2, null, 0),
            new MailFolderDto("projects", "Projects", 35, 1, null, 2),
            new MailFolderDto("proj-flightdeck", "FlightDeck", 20, 0, "projects", 0),
            new MailFolderDto("proj-flightmail", "MailDirector", 15, 1, "projects", 0),
            new MailFolderDto("notifications", "Notifications", 85, 0, null, 0),
            new MailFolderDto("personal", "Personal", 18, 0, null, 0)
        };

        _mockEmails = new List<EmailDto>
        {
            new EmailDto(
                "msg-1",
                "Jira Cloud",
                "jira@atlassian.net",
                "[JIRA] (FLIGHT-204) Implement standalone MailDirector WPF application",
                "Andrew assigned FLIGHT-204 to you. We need to extract the email module from FlightPlan into a modern WPF app using Wpf.Ui and FluentWindow.",
                DateTimeOffset.UtcNow.AddHours(-1),
                "https://outlook.office.com/mail/inbox/id/msg-1",
                "<div><h2>Issue Assigned: FLIGHT-204</h2><p><strong>Andrew Munro</strong> assigned <code>FLIGHT-204</code> to you.</p><p><strong>Summary:</strong> Implement standalone MailDirector WPF application</p><p><strong>Description:</strong> Move the email functionality from FlightPlan into its own new WPF app with Wpf.Ui, rule runner, and folder customizations.</p><hr/><p><a href='https://jira.example.com/browse/FLIGHT-204' style='background:#0052CC;color:white;padding:8px 16px;text-decoration:none;border-radius:4px;'>View Issue in Jira</a></p></div>",
                new List<string> { "Jira Notifications" },
                false,
                true,
                new List<string> { "Work", "Jira" }
            ),
            new EmailDto(
                "msg-2",
                "GitHub",
                "notifications@github.com",
                "[GitHub] Pull Request #88: Add WPF UI theming and folder manager",
                "Junie opened pull request #88 in ARmunro/sandbox: 'Add WPF UI theming and folder manager'. Please review the proposed changes.",
                DateTimeOffset.UtcNow.AddHours(-2),
                "https://outlook.office.com/mail/inbox/id/msg-2",
                "<div><h3>Pull Request #88 Opened</h3><p><strong>Junie</strong> wants to merge 12 commits into <code>main</code> from <code>feature/flightmail-wpf</code></p><p><strong>Changes:</strong></p><ul><li>WPF UI FluentWindow integration</li><li>Folder ordering, coloring, and icon customization</li><li>Email rule engine with multi-criteria filters</li></ul><br/><p><a href='https://github.com/ARmunro/sandbox/pull/88'>View Pull Request on GitHub</a></p></div>",
                new List<string> { "GitHub Activity" },
                false,
                false,
                new List<string> { "Dev" }
            ),
            new EmailDto(
                "msg-3",
                "TeamCity Server",
                "teamcity@ci.internal",
                "[TeamCity] Build Sandbox :: Full CI #142 PASSED",
                "Build Sandbox :: Full CI #142 successful in 4m 12s. All 58 unit tests passed.",
                DateTimeOffset.UtcNow.AddHours(-3),
                "https://outlook.office.com/mail/inbox/id/msg-3",
                "<div><h3 style='color:#28a745;'>✔ Build Passed: Sandbox :: Full CI #142</h3><p><strong>Triggered by:</strong> Git Commit (main branch)</p><p><strong>Duration:</strong> 4m 12s</p><p><strong>Tests:</strong> 58 passed, 0 failed, 0 ignored</p></div>",
                new List<string> { "TeamCity Builds" },
                true,
                false,
                new List<string> { "CI/CD" }
            ),
            new EmailDto(
                "msg-4",
                "Andrew Munro",
                "andrew@example.com",
                "FlightPlan Roadmap & Next Steps",
                "Hi team, just wanted to check in on our migration milestones. The new WPF client is looking really sharp!",
                DateTimeOffset.UtcNow.AddHours(-6),
                "https://outlook.office.com/mail/inbox/id/msg-4",
                "<div><p>Hi team,</p><p>Just wanted to check in on our migration milestones. The new WPF client is looking really sharp! Let's make sure the folder rules and customizations are fully covered in tests.</p><p>Cheers,<br/>Andrew</p></div>",
                new List<string>(),
                true,
                true,
                new List<string>()
            ),
            new EmailDto(
                "msg-5",
                "Azure Security Center",
                "alerts@azure.com",
                "[Security Alert] Unusual sign-in location detected",
                "Microsoft Defender for Cloud detected an unusual sign-in activity for your Azure tenant. Please review.",
                DateTimeOffset.UtcNow.AddDays(-1),
                "https://outlook.office.com/mail/inbox/id/msg-5",
                "<div><h2 style='color:#d9534f;'>Azure Security Alert</h2><p>Microsoft Defender detected an anomalous authentication event from IP <code>198.51.100.24</code>.</p><p><strong>Severity:</strong> Medium</p><p><strong>Resource:</strong> Azure AD Tenant</p></div>",
                new List<string> { "Security Reports" },
                false,
                false,
                new List<string> { "Security" }
            ),
            new EmailDto(
                "msg-6",
                "Cloud Services",
                "billing@cloudprovider.com",
                "Your monthly invoice for Sandbox Infrastructure",
                "Thank you for using Cloud Services. Your monthly invoice #INV-90214 is now available for download.",
                DateTimeOffset.UtcNow.AddDays(-2),
                "https://outlook.office.com/mail/inbox/id/msg-6",
                "<div><p>Dear Customer,</p><p>Your invoice for the period of this month is ready. Total due: <strong>$124.50</strong>.</p><p>Payment will be automatically processed via card ending in *4421.</p></div>",
                new List<string>(),
                true,
                false,
                new List<string>()
            ),
            new EmailDto(
                "msg-7",
                "Newsletter Daily",
                "digest@techdaily.io",
                "Tech Daily: The state of .NET 9 and Modern Windows Desktop Apps",
                "Welcome to today's digest covering C# 13 features, WPF UI developments, and developer productivity tools.",
                DateTimeOffset.UtcNow.AddDays(-3),
                "https://outlook.office.com/mail/inbox/id/msg-7",
                "<div><h1>Tech Daily</h1><p>Welcome to today's digest covering C# 13 features, WPF UI developments, and developer productivity tools.</p><h3>Top Stories</h3><ul><li>Building modern Fluent WPF applications with Wpf.Ui</li><li>Asynchronous stream processing in .NET 9</li><li>Optimizing memory allocations in desktop apps</li></ul></div>",
                new List<string> { "Newsletters" },
                false,
                false,
                new List<string>()
            )
        };

        // Initialize folder mappings
        _emailFolderMap["msg-1"] = "inbox";
        _emailFolderMap["msg-2"] = "inbox";
        _emailFolderMap["msg-3"] = "inbox";
        _emailFolderMap["msg-4"] = "inbox";
        _emailFolderMap["msg-5"] = "inbox";
        _emailFolderMap["msg-6"] = "inbox";
        _emailFolderMap["msg-7"] = "inbox";
    }

    public Task<IEnumerable<MailFolderDto>> GetMailFoldersAsync()
    {
        lock (_lock)
        {
            return Task.FromResult<IEnumerable<MailFolderDto>>(_mockFolders.ToList());
        }
    }

    public Task<IEnumerable<EmailDto>> GetEmailsAsync(string folderId = "inbox", int top = 50)
    {
        lock (_lock)
        {
            var targetFolder = folderId.ToLowerInvariant();
            var matches = _mockEmails
                .Where(e =>
                {
                    if (_emailFolderMap.TryGetValue(e.Id, out var fId))
                    {
                        return fId.Equals(targetFolder, StringComparison.OrdinalIgnoreCase);
                    }
                    return targetFolder == "inbox";
                })
                .OrderByDescending(e => e.ReceivedDateTime)
                .Take(top)
                .ToList();

            return Task.FromResult<IEnumerable<EmailDto>>(matches);
        }
    }

    public Task<EmailDto?> GetEmailAsync(string messageId)
    {
        lock (_lock)
        {
            var email = _mockEmails.FirstOrDefault(e => e.Id.Equals(messageId, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(email);
        }
    }

    public Task MoveEmailToDeletedItemsAsync(string messageId)
    {
        return MoveEmailToFolderAsync(messageId, "deleteditems");
    }

    public Task MoveEmailToFolderAsync(string messageId, string destinationFolderId)
    {
        lock (_lock)
        {
            _emailFolderMap[messageId] = destinationFolderId;
            return Task.CompletedTask;
        }
    }

    public Task ApplyRuleActionsAsync(string messageId, List<RuleAction> actions)
    {
        lock (_lock)
        {
            var idx = _mockEmails.FindIndex(e => e.Id.Equals(messageId, StringComparison.OrdinalIgnoreCase));
            if (idx == -1) return Task.CompletedTask;

            var email = _mockEmails[idx];
            bool isRead = email.IsRead;
            bool isFlagged = email.IsFlagged;
            var categories = email.Categories != null ? new List<string>(email.Categories) : new List<string>();

            foreach (var action in actions)
            {
                switch (action.Type)
                {
                    case ActionType.Star:
                        isFlagged = true;
                        break;
                    case ActionType.ClearFlag:
                        isFlagged = false;
                        break;
                    case ActionType.MarkAsRead:
                        isRead = true;
                        break;
                    case ActionType.AddCategory:
                        if (!string.IsNullOrWhiteSpace(action.Value) && !categories.Contains(action.Value, StringComparer.OrdinalIgnoreCase))
                        {
                            categories.Add(action.Value);
                        }
                        break;
                    case ActionType.Archive:
                        _emailFolderMap[messageId] = "archive";
                        break;
                    case ActionType.Move:
                        if (!string.IsNullOrWhiteSpace(action.Value))
                        {
                            var dest = action.Value.ToLowerInvariant().Replace(" ", "");
                            _emailFolderMap[messageId] = dest;
                        }
                        break;
                }
            }

            _mockEmails[idx] = email with
            {
                IsRead = isRead,
                IsFlagged = isFlagged,
                Categories = categories
            };

            return Task.CompletedTask;
        }
    }

    public void AddEmail(EmailDto email, string folderId = "inbox")
    {
        lock (_lock)
        {
            _mockEmails.Insert(0, email);
            _emailFolderMap[email.Id] = folderId;
        }
    }
}
