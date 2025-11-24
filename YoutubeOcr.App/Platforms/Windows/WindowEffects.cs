using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;
using YoutubeOcr.Core.Services;
using UiWindow = Microsoft.UI.Xaml.Window;

namespace YoutubeOcr.App.Platforms.Windows;

public static class WindowEffects
{
    // Default关闭玻璃特效，除非显式开启，避免不兼容的 GPU/驱动导致 WinUI 失稳。
    private static readonly bool DisableEffects =
        Environment.GetEnvironmentVariable("YOUTUBE_OCR_ENABLE_GLASS") != "1" &&
        !(AppContext.TryGetSwitch("YoutubeOcr.EnableGlass", out var enabled) && enabled);

    public static void Apply(UiWindow window)
    {
        if (DisableEffects || !IsBackdropSupported())
        {
            FileLogger.LogInfo("Glass effects disabled or not supported. Skipping WindowEffects.");
            return;
        }

        try
        {
            if (MicaController.IsSupported())
            {
                window.SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
            }
            else if (DesktopAcrylicController.IsSupported())
            {
                window.SystemBackdrop = new DesktopAcrylicBackdrop();
            }
        }
        catch (Exception ex)
        {
            FileLogger.LogError(ex, "Failed to apply backdrop.");
        }

        try
        {
            var hwnd = WindowNative.GetWindowHandle(window);
            var wid = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(wid);
            if (appWindow?.TitleBar is AppWindowTitleBar titleBar)
            {
                titleBar.ExtendsContentIntoTitleBar = true;
            }
        }
        catch (Exception ex)
        {
            FileLogger.LogError(ex, "Failed to configure title bar.");
        }
    }

    private static bool IsBackdropSupported()
    {
        // Guard known crash cases (older Windows builds or unsupported GPUs)
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            return false;
        }

        return MicaController.IsSupported() || DesktopAcrylicController.IsSupported();
    }
}
