using Microsoft.Maui.Platform;
using Windows.Storage.Pickers;
using WinRT.Interop;
using YoutubeOcr.Core.Services;

namespace YoutubeOcr.App.Platform;

public static class FolderPickerService
{
    public static async Task<string?> PickFolderAsync(string title)
    {
        try
        {
            var window = Application.Current?.Windows.FirstOrDefault();
            if (window?.Handler?.PlatformView is not MauiWinUIWindow mauiWindow)
            {
                return null;
            }

            var picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.Downloads,
                ViewMode = PickerViewMode.List
            };
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, mauiWindow.WindowHandle);
            var result = await picker.PickSingleFolderAsync();
            return result?.Path;
        }
        catch (Exception ex)
        {
            FileLogger.LogError(ex, "Folder picker failed");
            return null;
        }
    }
}
