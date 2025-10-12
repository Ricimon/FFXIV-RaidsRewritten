using Microsoft.Extensions.Logging;

namespace RaidsRewritten.Log;

public sealed class DalamudLoggerFactory(DalamudServices dalamud, Configuration configuration) : ILoggerFactory
{
    private readonly DalamudLogger logger = new(dalamud, configuration);

    public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
    {
        return this.logger;
    }

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }
}
