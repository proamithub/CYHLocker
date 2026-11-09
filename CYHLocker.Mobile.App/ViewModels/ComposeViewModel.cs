using System.Collections.ObjectModel;
using System.Windows.Input;
using CYHLocker.Mobile.App.Helpers;
using CYHLocker.Mobile.App.Models;
using CYHLocker.Mobile.App.Services;

namespace CYHLocker.Mobile.App.ViewModels;

public class ComposeViewModel : BaseViewModel
{
    private readonly ISmsService _smsService;
    private readonly ISmsStorageService _storageService;

    private string _recipient = string.Empty;
    private string _recipientName = string.Empty;
    private string _messageText = string.Empty;
    private string _counterText = "0 chars • 1 SMS (160 left) [GSM]";
    private int _selectedSimSlot = 0;

    public ObservableCollection<SimCardInfo> AvailableSims { get; } = new();
    public ObservableCollection<SmsQuickTemplate> Templates { get; } = new();

    public string Recipient
    {
        get => _recipient;
        set => SetProperty(ref _recipient, value);
    }

    public string RecipientName
    {
        get => _recipientName;
        set => SetProperty(ref _recipientName, value);
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

    public ComposeViewModel(ISmsService smsService, ISmsStorageService storageService)
    {
        _smsService = smsService;
        _storageService = storageService;
        Title = "Naya SMS";

        SendCommand = new Command(async () => await SendSmsAsync());
        TemplateSelectedCommand = new Command<SmsQuickTemplate>(OnTemplateSelected);

        _ = LoadInitDataAsync();
    }

    private async Task LoadInitDataAsync()
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

    private async Task SendSmsAsync()
    {
        if (string.IsNullOrWhiteSpace(Recipient))
        {
            await Shell.Current.DisplayAlertAsync("Validation", "Kripya recipient phone number daliye.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(MessageText))
        {
            await Shell.Current.DisplayAlertAsync("Validation", "Message khali nahi ho sakta.", "OK");
            return;
        }

        IsBusy = true;
        try
        {
            var res = await _smsService.SendSmsAsync(Recipient, MessageText, SelectedSimSlot);
            if (res.Success)
            {
                await Shell.Current.DisplayAlertAsync("Success", "SMS bhej diya gaya!", "OK");
                var destNumber = Recipient;
                var destName = RecipientName;
                Recipient = string.Empty;
                RecipientName = string.Empty;
                MessageText = string.Empty;

                // Navigate to chat
                await Shell.Current.GoToAsync($"chat?contactNumber={Uri.EscapeDataString(destNumber)}&contactName={Uri.EscapeDataString(destName)}");
            }
            else
            {
                await Shell.Current.DisplayAlertAsync("Error", res.Message, "OK");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
