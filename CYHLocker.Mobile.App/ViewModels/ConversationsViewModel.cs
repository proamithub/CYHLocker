using System.Collections.ObjectModel;
using System.Windows.Input;
using CYHLocker.Mobile.App.Models;
using CYHLocker.Mobile.App.Services;

namespace CYHLocker.Mobile.App.ViewModels;

public class ConversationsViewModel : BaseViewModel
{
    private readonly ISmsService _smsService;
    private string _searchQuery = string.Empty;
    private List<SmsConversationItem> _allConversations = new();

    public ObservableCollection<SmsConversationItem> FilteredConversations { get; } = new();

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ApplyFilter();
            }
        }
    }

    public ICommand LoadConversationsCommand { get; }
    public ICommand DeleteConversationCommand { get; }
    public ICommand SimulateIncomingSmsCommand { get; }

    public ConversationsViewModel(ISmsService smsService)
    {
        _smsService = smsService;
        Title = "Local SMS Messages";

        LoadConversationsCommand = new Command(async () => await LoadConversationsAsync());
        DeleteConversationCommand = new Command<SmsConversationItem>(async (conv) => await DeleteConversationAsync(conv));
        SimulateIncomingSmsCommand = new Command(async () => await SimulateIncomingAsync());

        _smsService.MessageReceived += OnSmsUpdated;
        _smsService.MessageSent += OnSmsUpdated;
    }

    private void OnSmsUpdated(object? sender, SmsMessageItem e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await LoadConversationsAsync();
        });
    }

    public async Task LoadConversationsAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            _allConversations = await _smsService.GetConversationsAsync();
            ApplyFilter();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        FilteredConversations.Clear();
        var query = SearchQuery?.Trim().ToLowerInvariant() ?? string.Empty;

        var items = string.IsNullOrEmpty(query)
            ? _allConversations
            : _allConversations.Where(c =>
                c.ContactNumber.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.ContactName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.LastMessageBody.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var item in items)
        {
            FilteredConversations.Add(item);
        }
    }

    private async Task DeleteConversationAsync(SmsConversationItem? conv)
    {
        if (conv == null) return;

        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Delete Chat",
            $"Kya aap {conv.DisplayTitle} ke saare SMS messages delete karna chahte hain?",
            "Delete",
            "Cancel");

        if (confirm)
        {
            await _smsService.DeleteConversationAsync(conv.ContactNumber);
            await LoadConversationsAsync();
        }
    }

    private async Task SimulateIncomingAsync()
    {
        string[] senders = ["+919876500111", "+919444332211", "BANK-ALERT", "+918888777766"];
        string[] names = ["Amit Kumar", "Neha Singh", "HDFC Security", "Rohit Verma"];
        string[] texts =
        [
            "Haan bhai, kya plan hai aaj sham ka?",
            "Aapka OTP hai 482901. Valid for 10 minutes.",
            "Documents check kar liye hain, sab sahi hai.",
            "Mujhe payment receive ho gayi hai, thank you!"
        ];

        var rnd = new Random();
        int idx = rnd.Next(senders.Length);

        await _smsService.SimulateIncomingSmsAsync(senders[idx], names[idx], texts[idx]);
    }
}
