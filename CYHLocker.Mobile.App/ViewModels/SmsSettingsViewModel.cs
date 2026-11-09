using System.Collections.ObjectModel;
using System.Windows.Input;
using CYHLocker.Mobile.App.Models;
using CYHLocker.Mobile.App.Services;

namespace CYHLocker.Mobile.App.ViewModels;

public class SmsSettingsViewModel : BaseViewModel
{
    private readonly ISmsService _smsService;
    private readonly ISmsStorageService _storageService;

    private bool _hasPermission;
    private string _permissionStatusText = "Checking...";
    private int _totalConversations;
    private int _totalMessages;
    private int _totalSent;
    private int _totalReceived;

    public ObservableCollection<SimCardInfo> SimCards { get; } = new();
    public ObservableCollection<SmsQuickTemplate> Templates { get; } = new();

    public bool HasPermission
    {
        get => _hasPermission;
        set => SetProperty(ref _hasPermission, value);
    }

    public string PermissionStatusText
    {
        get => _permissionStatusText;
        set => SetProperty(ref _permissionStatusText, value);
    }

    public int TotalConversations
    {
        get => _totalConversations;
        set => SetProperty(ref _totalConversations, value);
    }

    public int TotalMessages
    {
        get => _totalMessages;
        set => SetProperty(ref _totalMessages, value);
    }

    public int TotalSent
    {
        get => _totalSent;
        set => SetProperty(ref _totalSent, value);
    }

    public int TotalReceived
    {
        get => _totalReceived;
        set => SetProperty(ref _totalReceived, value);
    }

    public ICommand RequestPermissionCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand AddTemplateCommand { get; }

    public SmsSettingsViewModel(ISmsService smsService, ISmsStorageService storageService)
    {
        _smsService = smsService;
        _storageService = storageService;
        Title = "SIM & SMS Settings";

        RequestPermissionCommand = new Command(async () => await RequestPermissionsAsync());
        RefreshCommand = new Command(async () => await RefreshStatusAsync());
        AddTemplateCommand = new Command(async () => await AddTemplateAsync());

        _ = RefreshStatusAsync();
    }

    public async Task RefreshStatusAsync()
    {
        IsBusy = true;
        try
        {
            HasPermission = await _smsService.HasSmsPermissionAsync();
            PermissionStatusText = HasPermission ? "Granted (Active)" : "Permission Required";

            var sims = await _smsService.GetAvailableSimsAsync();
            SimCards.Clear();
            foreach (var s in sims)
                SimCards.Add(s);

            var convs = await _smsService.GetConversationsAsync();
            TotalConversations = convs.Count;

            int sent = 0;
            int received = 0;
            int total = 0;

            foreach (var c in convs)
            {
                total += c.Messages.Count;
                sent += c.Messages.Count(m => !m.IsIncoming);
                received += c.Messages.Count(m => m.IsIncoming);
            }

            TotalMessages = total;
            TotalSent = sent;
            TotalReceived = received;

            var tmpls = await _storageService.GetQuickTemplatesAsync();
            Templates.Clear();
            foreach (var t in tmpls)
                Templates.Add(t);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RequestPermissionsAsync()
    {
        var granted = await _smsService.RequestSmsPermissionAsync();
        HasPermission = granted;
        PermissionStatusText = granted ? "Granted (Active)" : "Denied / Not granted";

        if (granted)
        {
            await Shell.Current.DisplayAlertAsync("Permissions", "SMS Permissions mil chuki hain! SIM se direct messages bhej sakte hain.", "OK");
        }
        else
        {
            await Shell.Current.DisplayAlertAsync("Permissions", "SMS permission nahi mili. Settings me jaakar allow karein.", "OK");
        }
    }

    private async Task AddTemplateAsync()
    {
        string result = await Shell.Current.DisplayPromptAsync(
            "New Quick Template",
            "Canned SMS response likhiye:",
            "Add",
            "Cancel",
            "e.g. Call you back soon");

        if (!string.IsNullOrWhiteSpace(result))
        {
            await _storageService.AddQuickTemplateAsync(result.Trim());
            await RefreshStatusAsync();
        }
    }
}
