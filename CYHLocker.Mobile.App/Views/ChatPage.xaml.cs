using System.Collections.Specialized;
using CYHLocker.Mobile.App.ViewModels;

namespace CYHLocker.Mobile.App.Views;

public partial class ChatPage : ContentPage
{
    private readonly ChatViewModel _viewModel;

    public ChatPage(ChatViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _viewModel.Messages.CollectionChanged += OnMessagesChanged;
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && _viewModel.Messages.Count > 0)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                MessagesList.ScrollTo(_viewModel.Messages.Count - 1, position: ScrollToPosition.End, animate: true);
            });
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadMessagesAsync();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
