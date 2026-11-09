using CYHLocker.Mobile.App.Models;

namespace CYHLocker.Mobile.App.Services;

public interface ISmsService
{
    Task<bool> HasSmsPermissionAsync();
    Task<bool> RequestSmsPermissionAsync();
    Task<List<SimCardInfo>> GetAvailableSimsAsync();
    Task<SmsSendResult> SendSmsAsync(string recipientNumber, string messageText, int simSlot = 0);
    Task<List<SmsConversationItem>> GetConversationsAsync();
    Task<List<SmsMessageItem>> GetMessagesAsync(string contactNumber);
    Task<bool> DeleteConversationAsync(string contactNumber);
    Task<bool> DeleteMessageAsync(string messageId);
    Task SimulateIncomingSmsAsync(string fromNumber, string fromName, string body);

    event EventHandler<SmsMessageItem>? MessageReceived;
    event EventHandler<SmsMessageItem>? MessageSent;
}
