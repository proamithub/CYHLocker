using CYHLocker.Mobile.App.Services;
using CYHLocker.Mobile.App.ViewModels;
using CYHLocker.Mobile.App.Views;
using Microsoft.Extensions.Logging;

namespace CYHLocker.Mobile.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Services
		builder.Services.AddSingleton<ISmsStorageService, SmsStorageService>();
		builder.Services.AddSingleton<ISmsService, DefaultSmsService>();

		// ViewModels
		builder.Services.AddTransient<ConversationsViewModel>();
		builder.Services.AddTransient<ChatViewModel>();
		builder.Services.AddTransient<ComposeViewModel>();
		builder.Services.AddTransient<SmsSettingsViewModel>();

		// Views
		builder.Services.AddTransient<ConversationsPage>();
		builder.Services.AddTransient<ChatPage>();
		builder.Services.AddTransient<ComposePage>();
		builder.Services.AddTransient<SettingsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
