using CYHLocker.Mobile.App.ViewModels;

namespace CYHLocker.Mobile.App.Views;

public partial class ComposePage : ContentPage
{
    public ComposePage(ComposeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
