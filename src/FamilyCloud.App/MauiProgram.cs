using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using MudBlazor.Services;
using FamilyCloud.App.Services;

namespace FamilyCloud.App;

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
			});

		builder.Services.AddMauiBlazorWebView();
		builder.Services.AddMudServices();

		builder.Services.AddSingleton<AppAuthState>();
		builder.Services.AddSingleton<AppTitleState>();
		builder.Services.AddSingleton<AuthTokenStore>();
		builder.Services.AddSingleton<ServerAddressStore>();
		builder.Services.AddSingleton<SyncCoordinator>();
		builder.Services.AddTransient<AuthTokenHandler>();
		builder.Services.AddHttpClient<ApiClient>((sp, client) =>
			{
				var address = sp.GetRequiredService<ServerAddressStore>().Load();
				client.BaseAddress = address ?? new Uri(ServerConfig.DefaultBaseAddress);
			})
			.AddHttpMessageHandler<AuthTokenHandler>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		// The polling loop should only run while the app is actually on screen — pause it when the
		// user backgrounds the app so it doesn't keep hitting the server for nothing.
		MauiApp? mauiApp = null;
		builder.ConfigureLifecycleEvents(events =>
		{
#if ANDROID
			events.AddAndroid(android => android
				.OnResume(_ => mauiApp?.Services.GetService<SyncCoordinator>()?.Resume())
				.OnStop(_ => mauiApp?.Services.GetService<SyncCoordinator>()?.Pause()));
#endif
		});

		mauiApp = builder.Build();
		return mauiApp;
	}
}
