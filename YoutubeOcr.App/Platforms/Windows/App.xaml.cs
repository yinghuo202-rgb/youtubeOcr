using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Reflection;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace YoutubeOcr.App.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
/// executed, and as such is the logical equivalent of main() or WinMain().
/// </summary>
	public App()
	{
		TryBootstrap();
		this.InitializeComponent();
		this.UnhandledException += OnUnhandledException;
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
	{
		YoutubeOcr.Core.Services.FileLogger.LogError(e.Exception, "Xaml UnhandledException");
		e.Handled = true; // prevent WinUI failfast; app will try to stay alive
	}

	private static void TryBootstrap()
	{
		try
		{
			// Prefer the managed bootstrapper so we can invoke Initialize/Shutdown via reflection without a direct package reference.
			var dll = Path.Combine(AppContext.BaseDirectory, "Microsoft.WindowsAppRuntime.Bootstrap.Net.dll");
			if (!File.Exists(dll))
			{
				// Fallback to the native bootstrapper name in case the managed one is not present.
				dll = Path.Combine(AppContext.BaseDirectory, "Microsoft.WindowsAppRuntime.Bootstrap.dll");
				if (!File.Exists(dll)) return;
			}

			var asm = Assembly.LoadFrom(dll);
			var type = asm.GetType("Microsoft.WindowsAppRuntime.Bootstrap");
			if (type == null) return;

			// Try overload: Initialize(uint majorMinorVersion)
			var initWithVersion = type.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static, new[] { typeof(uint) });
			// Try overload: Initialize()
			var initNoArgs = type.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);
			var shutdown = type.GetMethod("Shutdown", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);

			if (initWithVersion != null)
			{
				initWithVersion.Invoke(null, new object[] { 0x00010007u }); // 1.7 major/minor
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
			// ignore bootstrap failures; app will just use system runtime if present
		}
	}
}

