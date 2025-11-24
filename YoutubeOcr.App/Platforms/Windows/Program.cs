using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using WinRT;
using YoutubeOcr.Core.Services;

namespace YoutubeOcr.App.WinUI;

public static class Program
{
	[STAThread]
	public static void Main(string[] args)
	{
		var localLogDir = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"YoutubeOcr",
			"Logs");
		FileLogger.Initialize(localLogDir);
		TryBootstrap();

		try
		{
			ComWrappersSupport.InitializeComWrappers();
		}
		catch
		{
			// Ignore COM wrapper initialization issues; WinUI will use default wrappers.
		}

		AppDomain.CurrentDomain.UnhandledException += (s, e) =>
		{
			FileLogger.LogError(e.ExceptionObject as Exception, "UnhandledException");
		};

		TaskScheduler.UnobservedTaskException += (s, e) =>
		{
			FileLogger.LogError(e.Exception, "UnobservedTaskException");
			e.SetObserved();
		};

		global::Microsoft.UI.Xaml.Application.Start((p) =>
		{
			var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
			SynchronizationContext.SetSynchronizationContext(context);
			try
			{
				new App();
			}
			catch (Exception ex)
			{
				FileLogger.LogError(ex, "Failed to start App");
				throw;
			}
		});
	}

	private static void TryBootstrap()
	{
		try
		{
			var baseDir = AppContext.BaseDirectory;
			var managed = Path.Combine(baseDir, "Microsoft.WindowsAppRuntime.Bootstrap.Net.dll");
			var native = Path.Combine(baseDir, "Microsoft.WindowsAppRuntime.Bootstrap.dll");
			var dll = File.Exists(managed) ? managed : native;
			if (!File.Exists(dll)) return;

			var asm = Assembly.LoadFrom(dll);
			var type = asm.GetType("Microsoft.WindowsAppRuntime.Bootstrap");
			if (type == null) return;

			var initWithVersion = type.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static, new[] { typeof(uint) });
			var initNoArgs = type.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);
			var shutdown = type.GetMethod("Shutdown", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);

			if (initWithVersion != null)
			{
				initWithVersion.Invoke(null, new object[] { 0x00010007u });
			}
			else
			{
				initNoArgs?.Invoke(null, null);
			}

			if (shutdown != null)
				AppDomain.CurrentDomain.ProcessExit += (_, __) => shutdown.Invoke(null, null);
		}
		catch
		{
			// Ignore bootstrap failures; app will try to use system runtime if present.
		}
	}
}
