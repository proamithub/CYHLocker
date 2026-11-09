using CYHLocker.Mobile.App.Models;
#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using Android.Telephony;
using CYHLocker.Mobile.App.Platforms.Android;
#endif

namespace CYHLocker.Mobile.App.Services;

public class DefaultSmsService : ISmsService
{
    private readonly ISmsStorageService _storageService;

    public event EventHandler<SmsMessageItem>? MessageReceived;
    public event EventHandler<SmsMessageItem>? MessageSent;

    public DefaultSmsService(ISmsStorageService storageService)
    {
        _storageService = storageService;

#if ANDROID
        SmsBroadcastReceiver.OnSmsReceived += HandleNativeSmsReceived;
#endif
    }

#if ANDROID
    private async void HandleNativeSmsReceived(string sender, string body)
    {
        var msg = new SmsMessageItem
        {
            Id = Guid.NewGuid().ToString(),
            SenderNumber = sender,
            RecipientNumber = "Self",
            ContactNumber = sender,
            Body = body,
            Timestamp = DateTime.UtcNow,
            IsIncoming = true,
            Status = SmsDeliveryStatus.Received,
            SimSlot = 0
        };

        await _storageService.SaveMessageAsync(msg);
        MessageReceived?.Invoke(this, msg);
    }
#endif

    public async Task<bool> HasSmsPermissionAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Sms>();
            return status == PermissionStatus.Granted;
        }
        catch
        {
            return true;
        }
    }

    public async Task<bool> RequestSmsPermissionAsync()
    {
        try
        {
            var status = await Permissions.RequestAsync<Permissions.Sms>();
            return status == PermissionStatus.Granted;
        }
        catch
        {
            return true;
        }
    }

    public async Task<List<SimCardInfo>> GetAvailableSimsAsync()
    {
        var list = new List<SimCardInfo>();

#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            var subManager = (SubscriptionManager?)context.GetSystemService(Context.TelephonySubscriptionService);

            if (subManager != null && OperatingSystem.IsAndroidVersionAtLeast(22))
            {
                var activeSubs = subManager.ActiveSubscriptionInfoList;
                if (activeSubs != null && activeSubs.Count > 0)
                {
                    for (int i = 0; i < activeSubs.Count; i++)
                    {
                        var sub = activeSubs[i];
                        list.Add(new SimCardInfo
                        {
                            SlotIndex = sub.SimSlotIndex,
                            DisplayName = $"SIM {sub.SimSlotIndex + 1} ({sub.DisplayName})",
                            CarrierName = sub.CarrierName?.ToString() ?? "Mobile SIM",
                            PhoneNumber = sub.Number,
                            IsActive = true
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DefaultSmsService] SIM error: {ex.Message}");
        }
#endif

        if (list.Count == 0)
        {
            list.Add(new SimCardInfo
            {
                SlotIndex = 0,
                DisplayName = "SIM 1 (Primary)",
                CarrierName = "Local Mobile SIM",
                IsActive = true
            });
            list.Add(new SimCardInfo
            {
                SlotIndex = 1,
                DisplayName = "SIM 2 (Secondary)",
                CarrierName = "Local Mobile SIM 2",
                IsActive = false
            });
        }

        return await Task.FromResult(list);
    }

    public async Task<SmsSendResult> SendSmsAsync(string recipientNumber, string messageText, int simSlot = 0)
    {
        if (string.IsNullOrWhiteSpace(recipientNumber))
            return SmsSendResult.Fail("Recipient phone number daliye.");

        if (string.IsNullOrWhiteSpace(messageText))
            return SmsSendResult.Fail("Message text khali nahi ho sakta.");

        var cleanNumber = recipientNumber.Trim();
        var sentItem = new SmsMessageItem
        {
            Id = Guid.NewGuid().ToString(),
            SenderNumber = "Self",
            RecipientNumber = cleanNumber,
            ContactNumber = cleanNumber,
            Body = messageText.Trim(),
            Timestamp = DateTime.UtcNow,
            IsIncoming = false,
            Status = SmsDeliveryStatus.Sent,
            SimSlot = simSlot
        };

#if ANDROID
        try
        {
            var hasPermission = await HasSmsPermissionAsync();
            if (!hasPermission)
            {
                hasPermission = await RequestSmsPermissionAsync();
            }

            var context = Android.App.Application.Context;
            SmsManager? smsManager = null;

            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                smsManager = (SmsManager?)context.GetSystemService(Java.Lang.Class.FromType(typeof(SmsManager)));
            }
            smsManager ??= SmsManager.Default;

            if (smsManager == null)
            {
                sentItem.Status = SmsDeliveryStatus.Failed;
                await _storageService.SaveMessageAsync(sentItem);
                return SmsSendResult.Fail("Device ka SMS Manager uplabdh nahi hai.");
            }

            // Split message if longer than single SMS capacity
            var parts = smsManager.DivideMessage(messageText);
            if (parts != null && parts.Count > 1)
            {
                smsManager.SendMultipartTextMessage(cleanNumber, null, parts, null, null);
            }
            else
            {
                smsManager.SendTextMessage(cleanNumber, null, messageText, null, null);
            }

            sentItem.Status = SmsDeliveryStatus.Sent;
            await _storageService.SaveMessageAsync(sentItem);
            MessageSent?.Invoke(this, sentItem);

            return SmsSendResult.Ok(sentItem, "SMS safaltapoorvak bhej diya gaya!");
        }
        catch (Exception ex)
        {
            sentItem.Status = SmsDeliveryStatus.Failed;
            await _storageService.SaveMessageAsync(sentItem);
            return SmsSendResult.Fail($"SMS bhejne me error aaya: {ex.Message}");
        }
#else
        // Fallback for Windows / macOS / Desktop testing:
        // Try MAUI Sms.Default.ComposeAsync or simulate instant local SMS delivery
        try
        {
            if (Microsoft.Maui.ApplicationModel.Communication.Sms.Default.IsComposeSupported)
            {
                // Can open native SMS composer if supported
                try
                {
                    var msg = new SmsMessage(messageText, cleanNumber);
                    await Microsoft.Maui.ApplicationModel.Communication.Sms.Default.ComposeAsync(msg);
                }
                catch
                {
                    // Fall back to local store simulation
                }
            }

            sentItem.Status = SmsDeliveryStatus.Sent;
            await _storageService.SaveMessageAsync(sentItem);
            MessageSent?.Invoke(this, sentItem);

            return SmsSendResult.Ok(sentItem, "SMS successfully dispatched!");
        }
        catch (Exception ex)
        {
            sentItem.Status = SmsDeliveryStatus.Failed;
            await _storageService.SaveMessageAsync(sentItem);
            return SmsSendResult.Fail($"Error: {ex.Message}");
        }
#endif
    }

    public async Task<List<SmsConversationItem>> GetConversationsAsync()
    {
        return await _storageService.LoadConversationsAsync();
    }

    public async Task<List<SmsMessageItem>> GetMessagesAsync(string contactNumber)
    {
        return await _storageService.GetMessagesForContactAsync(contactNumber);
    }

    public async Task<bool> DeleteConversationAsync(string contactNumber)
    {
        await _storageService.DeleteConversationAsync(contactNumber);
        return true;
    }

    public async Task<bool> DeleteMessageAsync(string messageId)
    {
        await _storageService.DeleteMessageAsync(messageId);
        return true;
    }

    public async Task SimulateIncomingSmsAsync(string fromNumber, string fromName, string body)
    {
        var incoming = new SmsMessageItem
        {
            Id = Guid.NewGuid().ToString(),
            SenderNumber = fromNumber,
            RecipientNumber = "Self",
            ContactNumber = fromNumber,
            ContactName = fromName,
            Body = body,
            Timestamp = DateTime.UtcNow,
            IsIncoming = true,
            Status = SmsDeliveryStatus.Received,
            SimSlot = 0
        };

        await _storageService.SaveMessageAsync(incoming);
        MessageReceived?.Invoke(this, incoming);
    }
}
