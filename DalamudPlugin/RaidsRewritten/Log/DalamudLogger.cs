using System;
using Dalamud.Game.Text;

namespace RaidsRewritten.Log;

public class DalamudLogger(DalamudServices dalamud, Configuration configuration) : ILogger, Microsoft.Extensions.Logging.ILogger
{
    public void Trace(string message, params object[] values)
    {
#if DEBUG
        dalamud.Log.Verbose(message, values);
        Log(LogLevel.Trace, message, values);
#endif
    }

    public void Debug(string message, params object[] values)
    {
        dalamud.Log.Debug(message, values);
        Log(LogLevel.Debug, message, values);
    }

    public void Info(string message, params object[] values)
    {
        dalamud.Log.Info(message, values);
        Log(LogLevel.Info, message, values);
    }

    public void Warn(string message, params object[] values)
    {
        dalamud.Log.Warning(message, values);
        Log(LogLevel.Warn, message, values);
    }

    public void Error(string message, params object[] values)
    {
        dalamud.Log.Error(message, values);
        Log(LogLevel.Error, message, values);
    }

    public void Fatal(string message, params object[] values)
    {
        dalamud.Log.Fatal(message, values);
        Log(LogLevel.Fatal, message, values);
    }

    private void Log(LogLevel logLevel, string message, params object[] values)
    {
        if (!configuration.PrintLogsToChat)
        {
            return;
        }

        if (logLevel.Ordinal < configuration.MinimumVisibleLogLevel)
        {
            return;
        }

        XivChatType chatType;

        if (logLevel.Ordinal <= LogLevel.Warn.Ordinal)
        {
            chatType = XivChatType.Debug;
        }
        else
        {
            chatType = XivChatType.ErrorMessage;
        }

        dalamud.ChatGui.Print(new XivChatEntry
        {
            Message = $"{logLevel} | {string.Format(message, values)}",
            Type = chatType
        });
    }

    void Microsoft.Extensions.Logging.ILogger.Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        string message = "[{0}: {1}] {2}";
        switch (logLevel)
        {
            case Microsoft.Extensions.Logging.LogLevel.Trace:
                Trace(message, eventId, logLevel, formatter(state, exception));
                break;
            case Microsoft.Extensions.Logging.LogLevel.Debug:
                Debug(message, eventId, logLevel, formatter(state, exception));
                break;
            case Microsoft.Extensions.Logging.LogLevel.Information:
                Info(message, eventId, logLevel, formatter(state, exception));
                break;
            case Microsoft.Extensions.Logging.LogLevel.Warning:
                Warn(message, eventId, logLevel, formatter(state, exception));
                break;
            case Microsoft.Extensions.Logging.LogLevel.Error:
                Error(message, eventId, logLevel, formatter(state, exception));
                break;
            case Microsoft.Extensions.Logging.LogLevel.Critical:
                Fatal(message, eventId, logLevel, formatter(state, exception));
                break;
            default:
                Error(message, eventId, logLevel, formatter(state, exception));
                break;
        }
    }

    bool Microsoft.Extensions.Logging.ILogger.IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

    IDisposable? Microsoft.Extensions.Logging.ILogger.BeginScope<TState>(TState state) => default!;
}
