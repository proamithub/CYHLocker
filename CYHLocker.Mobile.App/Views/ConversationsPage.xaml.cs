using CYHLocker.Mobile.App.Models;
using CYHLocker.Mobile.App.ViewModels;

namespace CYHLocker.Mobile.App.Views;

public partial class ConversationsPage : ContentPage
{
    private readonly ConversationsViewModel _viewModel;

    public ConversationsPage(ConversationsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadConversationsAsync();
    }

    private async void OnConversationTapped(object? sender, TappedEventArgs e)
    {
        if (sender is VisualElement element && element.BindingContext is SmsConversationItem item)
        {
            await Shell.Current.GoToAsync($"chat?contactNumber={Uri.EscapeDataString(item.ContactNumber)}&contactName={Uri.EscapeDataString(item.DisplayTitle)}");
        }
    }

    private async void OnComposeClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//compose");
    }
}
