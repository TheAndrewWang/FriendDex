using FriendDex.App.Services;
using FriendDex.Web.Services;
using Microsoft.Extensions.Logging;

namespace FriendDex.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder.UseMauiApp<App>();

		builder.Services.AddMauiBlazorWebView();

		builder.Services.AddSingleton<AppSettings>();
		builder.Services.AddSingleton<IServerConnection>(sp => sp.GetRequiredService<AppSettings>());
		// Sprite drawing on the server can take up to a minute, so allow a generous timeout.
		builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromMinutes(3) });
		builder.Services.AddSingleton<IFriendDexService, HttpFriendDexService>();
		builder.Services.AddSingleton<HttpAccountClient>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
