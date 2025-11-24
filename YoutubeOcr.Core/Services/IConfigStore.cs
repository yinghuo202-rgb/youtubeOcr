using System.Text.Json;
using YoutubeOcr.Core.Config;

namespace YoutubeOcr.Core.Services;

public interface IConfigStore
{
    string ConfigPath { get; }
    Task<PipelineConfig> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(PipelineConfig config, CancellationToken cancellationToken = default);
}

public class FileConfigStore : IConfigStore
{
    public string ConfigPath { get; }

    public FileConfigStore(string configPath)
    {
        ConfigPath = configPath;
    }

    public async Task<PipelineConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ConfigPath))
        {
            return new PipelineConfig();
        }

        try
        {
            await using var stream = File.OpenRead(ConfigPath);
            var config = await JsonSerializer.DeserializeAsync<PipelineConfig>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                },
                cancellationToken);

            return config ?? new PipelineConfig();
        }
        catch
        {
            // 配置损坏时回退到默认值，避免页面加载闪退。
            return new PipelineConfig();
        }
    }

    public async Task SaveAsync(PipelineConfig config, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Open(ConfigPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await System.Text.Json.JsonSerializer.SerializeAsync(
            stream,
            config,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true },
            cancellationToken);
    }
}
