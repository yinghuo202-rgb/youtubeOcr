using Microsoft.Extensions.DependencyInjection;

namespace YoutubeOcr.App;

public static class ServiceResolver
{
    public static IServiceProvider? Provider { get; private set; }

    public static void Initialize(IServiceProvider provider)
    {
        Provider = provider;
    }

    public static T Resolve<T>() where T : notnull
    {
        var provider = Provider ?? throw new InvalidOperationException("Service provider 尚未初始化");
        return provider.GetRequiredService<T>();
    }
}
