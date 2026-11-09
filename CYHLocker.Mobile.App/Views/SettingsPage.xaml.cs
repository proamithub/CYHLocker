using CYHLocker.Mobile.App.ViewModels;

namespace CYHLocker.Mobile.App.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SmsSettingsViewModel _viewModel;

    public SettingsPage(SmsSettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshStatusAsync();
    }
}
