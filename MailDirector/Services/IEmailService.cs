using System.Collections.Generic;
using System.Threading.Tasks;
using MailDirector.Models;

namespace MailDirector.Services;

public interface IEmailService
{
    Task<IEnumerable<MailFolderDto>> GetMailFoldersAsync();
    Task<IEnumerable<EmailDto>> GetEmailsAsync(string folderId = "inbox", int top = 50);
    Task<EmailDto?> GetEmailAsync(string messageId);
    Task MoveEmailToDeletedItemsAsync(string messageId);
    Task MoveEmailToFolderAsync(string messageId, string destinationFolderId);
    Task ApplyRuleActionsAsync(string messageId, List<RuleAction> actions);
}
