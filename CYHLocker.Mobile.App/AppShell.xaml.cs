using CYHLocker.Mobile.App.Views;

namespace CYHLocker.Mobile.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("chat", typeof(ChatPage));
    }
}
