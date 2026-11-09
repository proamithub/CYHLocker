using System.Text.Json;
using CYHLocker.Mobile.App.Models;

namespace CYHLocker.Mobile.App.Services;

public class SmsStoragePayload
{
    public List<SmsMessageItem> Messages { get; set; } = new();
    public List<SmsQuickTemplate> Templates { get; set; } = new();
}

public class SmsStorageService : ISmsStorageService
{
    private readonly string _storagePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private SmsStoragePayload? _cachedPayload;

    public SmsStorageService()
    {
        _storagePath = Path.Combine(FileSystem.AppDataDirectory, "cyh_local_sms_database.json");
    }

    private async Task<SmsStoragePayload> EnsureLoadedAsync()
    {
        if (_cachedPayload != null)
            return _cachedPayload;

        await _lock.WaitAsync();
        try
        {
            if (_cachedPayload != null)
                return _cachedPayload;

            if (File.Exists(_storagePath))
            {
                var json = await File.ReadAllTextAsync(_storagePath);
                var loaded = JsonSerializer.Deserialize<SmsStoragePayload>(json);
                if (loaded != null)
                {
                    _cachedPayload = loaded;
                    return _cachedPayload;
                }
            }

            // Seed initial data if fresh
            _cachedPayload = CreateSeedData();
            await SaveInternalAsync(_cachedPayload);
            return _cachedPayload;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveInternalAsync(SmsStoragePayload payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_storagePath, json);
    }

    public async Task<List<SmsConversationItem>> LoadConversationsAsync()
    {
        var data = await EnsureLoadedAsync();

        var groups = data.Messages
            .GroupBy(m => NormalizeNumber(m.ContactNumber))
            .ToList();

        var conversations = new List<SmsConversationItem>();

        foreach (var group in groups)
        {
            var sorted = group.OrderBy(m => m.Timestamp).ToList();
            var last = sorted.Last();
            var contactNumber = group.Key;
            var contactName = last.ContactName ?? sorted.FirstOrDefault(m => !string.IsNullOrEmpty(m.ContactName))?.ContactName ?? contactNumber;

            var conv = new SmsConversationItem
            {
                ContactNumber = contactNumber,
                ContactName = contactName,
                LastMessageBody = last.Body,
                LastMessageTime = last.Timestamp,
                UnreadCount = sorted.Count(m => m.IsIncoming && m.Status == SmsDeliveryStatus.Received),
                AvatarColor = SmsConversationItem.PickAvatarColor(contactNumber),
                Initials = SmsConversationItem.GenerateInitials(contactName, contactNumber)
            };

            foreach (var msg in sorted)
            {
                conv.Messages.Add(msg);
            }

            conversations.Add(conv);
        }

        return conversations.OrderByDescending(c => c.LastMessageTime).ToList();
    }

    public async Task<List<SmsMessageItem>> GetMessagesForContactAsync(string contactNumber)
    {
        var data = await EnsureLoadedAsync();
        var normalized = NormalizeNumber(contactNumber);
        return data.Messages
            .Where(m => NormalizeNumber(m.ContactNumber) == normalized)
            .OrderBy(m => m.Timestamp)
            .ToList();
    }

    public async Task SaveMessageAsync(SmsMessageItem message)
    {
        await _lock.WaitAsync();
        try
        {
            var data = await EnsureLoadedAsync();
            var existingIndex = data.Messages.FindIndex(m => m.Id == message.Id);
            if (existingIndex >= 0)
            {
                data.Messages[existingIndex] = message;
            }
            else
            {
                data.Messages.Add(message);
            }
            await SaveInternalAsync(data);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteMessageAsync(string messageId)
    {
        await _lock.WaitAsync();
        try
        {
            var data = await EnsureLoadedAsync();
            data.Messages.RemoveAll(m => m.Id == messageId);
            await SaveInternalAsync(data);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteConversationAsync(string contactNumber)
    {
        await _lock.WaitAsync();
        try
        {
            var data = await EnsureLoadedAsync();
            var normalized = NormalizeNumber(contactNumber);
            data.Messages.RemoveAll(m => NormalizeNumber(m.ContactNumber) == normalized);
            await SaveInternalAsync(data);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<SmsQuickTemplate>> GetQuickTemplatesAsync()
    {
        var data = await EnsureLoadedAsync();
        return data.Templates.ToList();
    }

    public async Task AddQuickTemplateAsync(string text, string category = "General")
    {
        await _lock.WaitAsync();
        try
        {
            var data = await EnsureLoadedAsync();
            data.Templates.Add(new SmsQuickTemplate { Text = text, Category = category });
            await SaveInternalAsync(data);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string NormalizeNumber(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
            return string.Empty;

        var clean = new string(number.Where(c => char.IsDigit(c) || c == '+').ToArray());
        return clean.Trim();
    }

    private static SmsStoragePayload CreateSeedData()
    {
        var now = DateTime.UtcNow;
        return new SmsStoragePayload
        {
            Templates =
            [
                new() { Text = "Main raste me hu, 10 min me call karta hu.", Category = "Driving" },
                new() { Text = "Haan theek hai, confirm ho gaya.", Category = "Quick" },
                new() { Text = "Message mil gaya, shukriya!", Category = "Polite" },
                new() { Text = "Kripya apna address ya location send kar dijiye.", Category = "Work" },
                new() { Text = "Main meeting me hu, thodi der me baat karte hain.", Category = "Meeting" }
            ],
            Messages =
            [
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    SenderNumber = "+919876543210",
                    RecipientNumber = "Self",
                    ContactNumber = "+919876543210",
                    ContactName = "Rahul Sharma",
                    Body = "Namaste! Kya documents ready hain?",
                    Timestamp = now.AddHours(-3),
                    IsIncoming = true,
                    Status = SmsDeliveryStatus.Received,
                    SimSlot = 0
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    SenderNumber = "Self",
                    RecipientNumber = "+919876543210",
                    ContactNumber = "+919876543210",
                    ContactName = "Rahul Sharma",
                    Body = "Haan Rahul ji, documents scan ho chuke hain, abhi details bhejta hu.",
                    Timestamp = now.AddHours(-2),
                    IsIncoming = false,
                    Status = SmsDeliveryStatus.Delivered,
                    SimSlot = 0
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    SenderNumber = "+919812345678",
                    RecipientNumber = "Self",
                    ContactNumber = "+919812345678",
                    ContactName = "Priya Verma",
                    Body = "Meeting ka time kya fix hua hai?",
                    Timestamp = now.AddDays(-1).AddHours(2),
                    IsIncoming = true,
                    Status = SmsDeliveryStatus.Received,
                    SimSlot = 0
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    SenderNumber = "CYH-ALERT",
                    RecipientNumber = "Self",
                    ContactNumber = "CYH-ALERT",
                    ContactName = "CYH Security System",
                    Body = "CYHLocker SMS App ready hai! Aap local SIM se text message bhej aur receive kar sakte hain.",
                    Timestamp = now.AddMinutes(-15),
                    IsIncoming = true,
                    Status = SmsDeliveryStatus.Received,
                    SimSlot = 0
                }
            ]
        };
    }
}
