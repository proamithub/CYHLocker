using System.Collections.ObjectModel;
using System.Windows.Input;
using CYHLocker.Mobile.App.Helpers;
using CYHLocker.Mobile.App.Models;
using CYHLocker.Mobile.App.Services;

namespace CYHLocker.Mobile.App.ViewModels;

[QueryProperty(nameof(ContactNumber), "contactNumber")]
[QueryProperty(nameof(ContactName), "contactName")]
public class ChatViewModel : BaseViewModel
{
    private readonly ISmsService _smsService;
    private readonly ISmsStorageService _storageService;

    private string _contactNumber = string.Empty;
    private string _contactName = string.Empty;
    private string _messageText = string.Empty;
    private string _counterText = "0 chars • 1 SMS (160 left) [GSM]";
    private int _selectedSimSlot = 0;

    public ObservableCollection<SmsMessageItem> Messages { get; } = new();
    public ObservableCollection<SmsQuickTemplate> Templates { get; } = new();
    public ObservableCollection<SimCardInfo> AvailableSims { get; } = new();

    public string ContactNumber
    {
        get => _contactNumber;
        set
        {
            if (SetProperty(ref _contactNumber, value))
            {
                Title = string.IsNullOrWhiteSpace(_contactName) ? _contactNumber : _contactName;
                _ = LoadMessagesAsync();
            }
        }
    }

    public string ContactName
    {
        get => _contactName;
        set
        {
            if (SetProperty(ref _contactName, value))
            {
                Title = string.IsNullOrWhiteSpace(_contactName) ? _contactNumber : _contactName;
            }
        }
    }

    public string MessageText
    {
        get => _messageText;
        set
        {
            if (SetProperty(ref _messageText, value))
            {
                CounterText = SmsSegmentCalculator.FormatCounter(value);
            }
        }
    }

    public string CounterText
    {
        get => _counterText;
        set => SetProperty(ref _counterText, value);
    }

    public int SelectedSimSlot
    {
        get => _selectedSimSlot;
        set => SetProperty(ref _selectedSimSlot, value);
    }

    public ICommand SendCommand { get; }
    public ICommand TemplateSelectedCommand { get; }
    public ICommand LoadMessagesCommand { get; }
    public ICommand DeleteMessageCommand { get; }

    public ChatViewModel(ISmsService smsService, ISmsStorageService storageService)
    {
        _smsService = smsService;
        _storageService = storageService;

        SendCommand = new Command(async () => await SendMessageAsync(), () => !string.IsNullOrWhiteSpace(MessageText));
        TemplateSelectedCommand = new Command<SmsQuickTemplate>(OnTemplateSelected);
        LoadMessagesCommand = new Command(async () => await LoadMessagesAsync());
        DeleteMessageCommand = new Command<SmsMessageItem>(async (m) => await DeleteMessageAsync(m));

        _smsService.MessageReceived += OnSmsReceived;
        _smsService.MessageSent += OnSmsSent;

        _ = LoadSimsAndTemplatesAsync();
    }

    private async Task LoadSimsAndTemplatesAsync()
    {
        var sims = await _smsService.GetAvailableSimsAsync();
        AvailableSims.Clear();
        foreach (var s in sims)
            AvailableSims.Add(s);

        var tmpls = await _storageService.GetQuickTemplatesAsync();
        Templates.Clear();
        foreach (var t in tmpls)
            Templates.Add(t);
    }

    private void OnTemplateSelected(SmsQuickTemplate? template)
    {
        if (template != null)
        {
            MessageText = template.Text;
        }
    }

    public async Task LoadMessagesAsync()
    {
        if (string.IsNullOrWhiteSpace(ContactNumber)) return;

        IsBusy = true;
        try
        {
            var list = await _smsService.GetMessagesAsync(ContactNumber);
            Messages.Clear();
            foreach (var m in list)
            {
                Messages.Add(m);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(MessageText)) return;

        var textToSend = MessageText;
        MessageText = string.Empty;

        var result = await _smsService.SendSmsAsync(ContactNumber, textToSend, SelectedSimSlot);

        if (!result.Success)
        {
            await Shell.Current.DisplayAlertAsync("SMS Error", result.Message, "OK");
        }
        else
        {
            await LoadMessagesAsync();
        }
    }

    private async Task DeleteMessageAsync(SmsMessageItem? item)
    {
        if (item == null) return;

        await _smsService.DeleteMessageAsync(item.Id);
        Messages.Remove(item);
    }

    private void OnSmsReceived(object? sender, SmsMessageItem e)
    {
        if (e.ContactNumber.Contains(ContactNumber, StringComparison.OrdinalIgnoreCase) ||
            ContactNumber.Contains(e.ContactNumber, StringComparison.OrdinalIgnoreCase))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Messages.Add(e);
            });
        }
    }

    private void OnSmsSent(object? sender, SmsMessageItem e)
    {
        if (e.ContactNumber.Contains(ContactNumber, StringComparison.OrdinalIgnoreCase) ||
            ContactNumber.Contains(e.ContactNumber, StringComparison.OrdinalIgnoreCase))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!Messages.Any(m => m.Id == e.Id))
                {
                    Messages.Add(e);
                }
            });
        }
    }
}
