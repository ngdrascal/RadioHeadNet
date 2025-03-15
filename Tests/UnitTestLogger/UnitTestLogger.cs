using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace UnitTestLogger;

[ExcludeFromCodeCoverage]
public class UnitTestLogger : ILogger
{
    private readonly string _name;
    private readonly Func<UnitTestLoggerConfiguration> _getCurrentConfig;

    public UnitTestLogger(string name, Func<UnitTestLoggerConfiguration> getCurrentConfig)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _getCurrentConfig = getCurrentConfig ?? throw new ArgumentNullException(nameof(getCurrentConfig));
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => default!;

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        if (_getCurrentConfig().ShowLogLevel)
            message = $"{GetLogLevelString(logLevel)}: {_name}: {message}";

        if (exception != null)
            message += $"{Environment.NewLine}{exception}";

        Console.WriteLine(message);
    }

    private string GetLogLevelString(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "TRCE",
            LogLevel.Debug => "DBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERRO",
            LogLevel.Critical => "CRIT",
            LogLevel.None => "NONE",
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel))
        };
    }
}
