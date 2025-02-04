using System.Diagnostics.CodeAnalysis;

namespace UnitTestLogger;

[ExcludeFromCodeCoverage]
public class UnitTestLoggerConfiguration
{
    public bool ShowLogLevel { get; set; } = true;
}