using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;

namespace UnitTestLogger;

public static class UnitTestLoggerFactoryExtensions
{
    public static ILoggingBuilder AddUnitTestLogger(this ILoggingBuilder builder)
    {
        _ = builder ?? throw new ArgumentNullException(nameof(builder));

        builder.AddConfiguration();
    
        builder.Services
            .TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, UnitTestLoggerProvider>());

        LoggerProviderOptions.RegisterProviderOptions
            <UnitTestLoggerConfiguration, UnitTestLoggerProvider>(services: builder.Services);

        return builder;
    }
    
    public static ILoggingBuilder AddUnitTestLogger(this ILoggingBuilder builder,
        Action<UnitTestLoggerConfiguration> configuration)
    {
        builder.AddUnitTestLogger();
        builder.Services.Configure(configuration);
    
        return builder;
    }
}
