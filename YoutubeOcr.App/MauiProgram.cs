using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using YoutubeOcr.Core.Config;
using YoutubeOcr.Core.Services;

namespace YoutubeOcr.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        FileLogger.Initialize(Path.Combine(FileSystem.AppDataDirectory, "Logs"));

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var configPath = Path.Combine(FileSystem.AppDataDirectory, PipelineConfig.DefaultConfigFileName);
        builder.Services.AddSingleton(new ToolLocator());
        builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
        builder.Services.AddSingleton<IConfigStore>(_ => new FileConfigStore(configPath));
        builder.Services.AddSingleton<IYouTubeDownloader, YouTubeDownloader>();
        builder.Services.AddSingleton<IFrameExtractor, FfmpegFrameExtractor>();
        builder.Services.AddSingleton<IOcrEngine, WindowsOcrEngine>();
        builder.Services.AddSingleton<IResultExporter, ResultExporter>();
        builder.Services.AddSingleton<PipelineState>();
        builder.Services.AddSingleton<MainPage>();

        var app = builder.Build();
        ServiceResolver.Initialize(app.Services);
        return app;
    }
}
