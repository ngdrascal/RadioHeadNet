using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UnitTestLogger;

public class UnitTestLoggerProvider : ILoggerProvider
{
    private readonly UnitTestLoggerConfiguration _currentConfig;
    private readonly ConcurrentDictionary<string, UnitTestLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);

    private UnitTestLoggerConfiguration GetCurrentConfig()
    {
        return _currentConfig;
    }

    public UnitTestLoggerProvider(IOptionsMonitor<UnitTestLoggerConfiguration> config)
    {
        _currentConfig = config.CurrentValue;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, _ => new UnitTestLogger(categoryName, GetCurrentConfig));
    }

    public void Dispose()
    {
        _loggers.Clear();
    }
}
