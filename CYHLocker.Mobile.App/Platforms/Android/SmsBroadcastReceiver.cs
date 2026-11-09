#if ANDROID
using Android.App;
using Android.Content;
using Android.Provider;
using Android.Telephony;

namespace CYHLocker.Mobile.App.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = true, Permission = "android.permission.BROADCAST_SMS")]
[IntentFilter(new[] { "android.provider.Telephony.SMS_RECEIVED" }, Priority = (int)IntentFilterPriority.HighPriority)]
public class SmsBroadcastReceiver : BroadcastReceiver
{
    public static event Action<string, string>? OnSmsReceived;

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action != "android.provider.Telephony.SMS_RECEIVED")
            return;

        try
        {
            var messages = Telephony.Sms.Intents.GetMessagesFromIntent(intent);
            if (messages == null || messages.Length == 0)
                return;

            string sender = messages[0].DisplayOriginatingAddress ?? string.Empty;
            var bodyBuilder = new System.Text.StringBuilder();

            foreach (var message in messages)
            {
                if (message != null)
                {
                    bodyBuilder.Append(message.DisplayMessageBody);
                }
            }

            var fullBody = bodyBuilder.ToString();
            if (!string.IsNullOrEmpty(sender) && !string.IsNullOrEmpty(fullBody))
            {
                OnSmsReceived?.Invoke(sender, fullBody);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SmsBroadcastReceiver] Error: {ex.Message}");
        }
    }
}
#endif
