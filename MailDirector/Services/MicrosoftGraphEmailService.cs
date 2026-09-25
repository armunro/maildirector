using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Identity;
using MailDirector.Models;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace MailDirector.Services;

public class MicrosoftGraphEmailService : IEmailService
{
    private readonly MicrosoftGraphConfig _config;
    private readonly IStorageService _storageService;
    private GraphServiceClient? _graphClient;

    public MicrosoftGraphEmailService(MicrosoftGraphConfig config, IStorageService storageService)
    {
        _config = config;
        _storageService = storageService;
    }

    private string AuthRecordPath => _storageService.GetAuthRecordPath();

    private async Task<GraphServiceClient> GetClientAsync()
    {
        if (_graphClient != null) return _graphClient;

        var scopes = new[] { "Mail.Read", "Mail.ReadWrite", "User.Read", "Offline_Access" };
        var options = new InteractiveBrowserCredentialOptions
        {
            TenantId = _config.TenantId,
            ClientId = _config.ClientId,
            RedirectUri = new Uri("http://localhost"),
            TokenCachePersistenceOptions = new TokenCachePersistenceOptions()
        };

        if (File.Exists(AuthRecordPath))
        {
            try
            {
                using var authRecordStream = new FileStream(AuthRecordPath, FileMode.Open, FileAccess.Read);
                options.AuthenticationRecord = await AuthenticationRecord.DeserializeAsync(authRecordStream);
            }
            catch
            {
                // Ignore corrupt token cache
            }
        }

        var credential = new InteractiveBrowserCredential(options);

        if (options.AuthenticationRecord == null)
        {
            var authRecord = await credential.AuthenticateAsync(new Azure.Core.TokenRequestContext(scopes));
            using var authRecordStream = new FileStream(AuthRecordPath, FileMode.Create, FileAccess.Write);
            await authRecord.SerializeAsync(authRecordStream);
        }

        _graphClient = new GraphServiceClient(credential, scopes);
        return _graphClient;
    }

    public async Task<IEnumerable<MailFolderDto>> GetMailFoldersAsync()
    {
        try
        {
            var client = await GetClientAsync();
            var allFolders = new List<MailFolderDto>();

            var topLevelFolders = await client.Me.MailFolders.GetAsync(config =>
            {
                config.QueryParameters.Top = 100;
            });

            if (topLevelFolders?.Value != null)
            {
                foreach (var folder in topLevelFolders.Value)
                {
                    await ProcessFolderAsync(client, folder, allFolders);
                }
            }

            return allFolders;
        }
        catch
        {
            return Enumerable.Empty<MailFolderDto>();
        }
    }

    private async Task ProcessFolderAsync(GraphServiceClient client, MailFolder folder, List<MailFolderDto> allFolders)
    {
        if (allFolders.Any(f => f.Id == folder.Id)) return;

        allFolders.Add(new MailFolderDto(
            folder.Id ?? "",
            folder.DisplayName ?? "Unknown",
            folder.TotalItemCount,
            folder.UnreadItemCount,
            folder.ParentFolderId,
            folder.ChildFolderCount
        ));

        if (folder.ChildFolderCount > 0)
        {
            var childFolders = await client.Me.MailFolders[folder.Id].ChildFolders.GetAsync(config =>
            {
                config.QueryParameters.Top = 100;
            });

            if (childFolders?.Value != null)
            {
                foreach (var child in childFolders.Value)
                {
                    await ProcessFolderAsync(client, child, allFolders);
                }
            }
        }
    }

    public async Task<IEnumerable<EmailDto>> GetEmailsAsync(string folderId = "inbox", int top = 50)
    {
        try
        {
            var client = await GetClientAsync();
            var messages = await client.Me.MailFolders[folderId].Messages
                .GetAsync(config =>
                {
                    config.QueryParameters.Top = top;
                    config.QueryParameters.Select = new[] { "id", "subject", "from", "receivedDateTime", "bodyPreview", "webLink", "body", "isRead", "flag", "categories" };
                    config.QueryParameters.Orderby = new[] { "receivedDateTime desc" };
                });

            return messages?.Value?.Select(m => new EmailDto(
                m.Id ?? "",
                m.From?.EmailAddress?.Name ?? m.From?.EmailAddress?.Address ?? "Unknown",
                m.From?.EmailAddress?.Address ?? "",
                m.Subject ?? "(No Subject)",
                m.BodyPreview ?? "",
                m.ReceivedDateTime ?? DateTimeOffset.MinValue,
                m.WebLink ?? "",
                m.Body?.Content,
                null,
                m.IsRead ?? false,
                m.Flag?.FlagStatus == FollowupFlagStatus.Flagged,
                m.Categories?.ToList()
            )) ?? Enumerable.Empty<EmailDto>();
        }
        catch
        {
            return Enumerable.Empty<EmailDto>();
        }
    }

    public async Task<EmailDto?> GetEmailAsync(string messageId)
    {
        try
        {
            var client = await GetClientAsync();
            var m = await client.Me.Messages[messageId]
                .GetAsync(config =>
                {
                    config.QueryParameters.Select = new[] { "id", "subject", "from", "receivedDateTime", "bodyPreview", "webLink", "body", "isRead", "flag", "categories" };
                });

            if (m == null) return null;

            return new EmailDto(
                m.Id ?? "",
                m.From?.EmailAddress?.Name ?? m.From?.EmailAddress?.Address ?? "Unknown",
                m.From?.EmailAddress?.Address ?? "",
                m.Subject ?? "(No Subject)",
                m.BodyPreview ?? "",
                m.ReceivedDateTime ?? DateTimeOffset.MinValue,
                m.WebLink ?? "",
                m.Body?.Content,
                null,
                m.IsRead ?? false,
                m.Flag?.FlagStatus == FollowupFlagStatus.Flagged,
                m.Categories?.ToList()
            );
        }
        catch
        {
            return null;
        }
    }

    public async Task MoveEmailToDeletedItemsAsync(string messageId)
    {
        var client = await GetClientAsync();
        var moveRequest = new Microsoft.Graph.Me.Messages.Item.Move.MovePostRequestBody { DestinationId = "deleteditems" };
        await client.Me.Messages[messageId].Move.PostAsync(moveRequest);
    }

    public async Task MoveEmailToFolderAsync(string messageId, string destinationFolderId)
    {
        var client = await GetClientAsync();
        var moveRequest = new Microsoft.Graph.Me.Messages.Item.Move.MovePostRequestBody { DestinationId = destinationFolderId };
        await client.Me.Messages[messageId].Move.PostAsync(moveRequest);
    }

    public async Task ApplyRuleActionsAsync(string messageId, List<RuleAction> actions)
    {
        var client = await GetClientAsync();
        var updateMessage = new Message();
        bool needsUpdate = false;
        string? destinationId = null;

        foreach (var action in actions)
        {
            switch (action.Type)
            {
                case ActionType.Star:
                    updateMessage.Flag = new FollowupFlag { FlagStatus = FollowupFlagStatus.Flagged };
                    needsUpdate = true;
                    break;
                case ActionType.ClearFlag:
                    updateMessage.Flag = new FollowupFlag { FlagStatus = FollowupFlagStatus.NotFlagged };
                    needsUpdate = true;
                    break;
                case ActionType.MarkAsRead:
                    updateMessage.IsRead = true;
                    needsUpdate = true;
                    break;
                case ActionType.AddCategory:
                    if (!string.IsNullOrWhiteSpace(action.Value))
                    {
                        var currentMessage = await client.Me.Messages[messageId].GetAsync(c => c.QueryParameters.Select = new[] { "categories" });
                        var categories = currentMessage?.Categories?.ToList() ?? new List<string>();
                        if (!categories.Contains(action.Value, StringComparer.OrdinalIgnoreCase))
                        {
                            categories.Add(action.Value);
                            updateMessage.Categories = categories;
                            needsUpdate = true;
                        }
                    }
                    break;
                case ActionType.Archive:
                    destinationId = "archive";
                    break;
                case ActionType.Move:
                    if (!string.IsNullOrWhiteSpace(action.Value))
                    {
                        destinationId = await GetFolderIdByPathAsync(action.Value);
                    }
                    break;
            }
        }

        if (needsUpdate)
        {
            await client.Me.Messages[messageId].PatchAsync(updateMessage);
        }

        if (destinationId != null)
        {
            await client.Me.Messages[messageId].Move.PostAsync(new Microsoft.Graph.Me.Messages.Item.Move.MovePostRequestBody { DestinationId = destinationId });
        }
    }

    private async Task<string?> GetFolderIdByPathAsync(string path)
    {
        var client = await GetClientAsync();
        var folderNames = path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        string? currentFolderId = null;

        foreach (var folderName in folderNames)
        {
            MailFolderCollectionResponse? folders;
            if (currentFolderId == null)
            {
                folders = await client.Me.MailFolders.GetAsync(config =>
                {
                    config.QueryParameters.Filter = $"displayName eq '{folderName.Replace("'", "''")}'";
                    config.QueryParameters.Top = 1;
                });
            }
            else
            {
                folders = await client.Me.MailFolders[currentFolderId].ChildFolders.GetAsync(config =>
                {
                    config.QueryParameters.Filter = $"displayName eq '{folderName.Replace("'", "''")}'";
                    config.QueryParameters.Top = 1;
                });
            }

            var match = folders?.Value?.FirstOrDefault();
            if (match == null) return null;
            currentFolderId = match.Id;
        }

        return currentFolderId;
    }
}
