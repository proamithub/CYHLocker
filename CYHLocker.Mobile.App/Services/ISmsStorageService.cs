using CYHLocker.Mobile.App.Models;

namespace CYHLocker.Mobile.App.Services;

public interface ISmsStorageService
{
    Task<List<SmsConversationItem>> LoadConversationsAsync();
    Task<List<SmsMessageItem>> GetMessagesForContactAsync(string contactNumber);
    Task SaveMessageAsync(SmsMessageItem message);
    Task DeleteMessageAsync(string messageId);
    Task DeleteConversationAsync(string contactNumber);
    Task<List<SmsQuickTemplate>> GetQuickTemplatesAsync();
    Task AddQuickTemplateAsync(string text, string category = "General");
}
