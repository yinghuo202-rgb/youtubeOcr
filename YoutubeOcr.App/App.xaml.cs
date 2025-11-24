using System.Runtime.ExceptionServices;
using YoutubeOcr.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace YoutubeOcr.App;

public partial class App : Application
{
	public App()
	{
		HookUnhandledExceptions();
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(ResolveMainPage());
	}

	private static void HookUnhandledExceptions()
	{
		try
		{
#if WINDOWS
			var winApp = global::Microsoft.UI.Xaml.Application.Current;
			if (winApp != null)
			{
				winApp.UnhandledException += (_, e) =>
				{
					FileLogger.LogError(e.Exception, "XAML UnhandledException");
					// prevent WinUI failfast
					e.Handled = true;
				};
			}
#endif

			AppDomain.CurrentDomain.FirstChanceException += (_, args) =>
			{
				// lightweight trace to help locate startup failures
				FileLogger.LogError(args.Exception, "FirstChanceException");
			};
		}
		catch
		{
			// avoid crashing while wiring diagnostics
		}
	}

	private static MainPage ResolveMainPage()
	{
		try
		{
			var provider = ServiceResolver.Provider;
			if (provider != null)
			{
				var page = provider.GetService<MainPage>();
				if (page != null) return page;
			}
		}
		catch
		{
			// fallback below
		}
		// fallback manual construction
		var locator = new ToolLocator();
		var runnerFallback = new ProcessRunner();
		return new MainPage(new YouTubeDownloader(locator, runnerFallback),
			new FfmpegFrameExtractor(locator, runnerFallback),
			new WindowsOcrEngine());
	}
}
