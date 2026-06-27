using Microsoft.Extensions.DependencyInjection;

namespace RssBandit.Maui;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		UserAppTheme = AppTheme.Dark; // TokyoNight is a dark theme -- force it regardless of the OS setting.
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}