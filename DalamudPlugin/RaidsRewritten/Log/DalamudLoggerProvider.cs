using Microsoft.Extensions.Logging;

namespace RaidsRewritten.Log;

public sealed class DalamudLoggerProvider(DalamudServices dalamud, Configuration configuration) : ILoggerProvider
{
    private readonly DalamudLogger logger = new(dalamud, configuration);

    public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
    {
        return this.logger;
    }

    public void Dispose()
    {
    }
}
