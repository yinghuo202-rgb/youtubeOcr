using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;
using System.Runtime.ExceptionServices;
using YoutubeOcr.Core.Services;

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
			AppDomain.CurrentDomain.UnhandledException += (_, e) =>
			{
				var ex = e.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception object");
				WriteUnhandledLog("AppDomain", ex);
			};

			TaskScheduler.UnobservedTaskException += (_, e) =>
			{
				WriteUnhandledLog("TaskScheduler", e.Exception);
				e.SetObserved();
			};

#if WINDOWS
			var winApp = global::Microsoft.UI.Xaml.Application.Current;
			if (winApp != null)
			{
				winApp.UnhandledException += (_, e) =>
				{
					WriteUnhandledLog("WinUI", e.Exception);
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

	private static void WriteUnhandledLog(string source, Exception? ex)
	{
		try
		{
			var logDir = Path.Combine(FileSystem.AppDataDirectory, "Logs");
			Directory.CreateDirectory(logDir);
			var logPath = Path.Combine(logDir, "unhandled.log");
			var content = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{source}] {ex}";
			File.AppendAllText(logPath, content + Environment.NewLine);
			FileLogger.LogError(ex, $"Unhandled: {source}");
		}
		catch
		{
			// last-resort logging should never crash the app
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
