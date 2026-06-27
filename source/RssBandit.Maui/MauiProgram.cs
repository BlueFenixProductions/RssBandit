using Microsoft.Extensions.Logging;

namespace RssBandit.Maui;

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
				// Hack -- the reading font for the app (and the article WebView via @font-face).
				fonts.AddFont("Hack-Regular.ttf", "HackRegular");
				fonts.AddFont("Hack-Bold.ttf", "HackBold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
