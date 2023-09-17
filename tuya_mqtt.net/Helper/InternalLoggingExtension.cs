using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Configuration;

namespace tuya_mqtt.net.Helper
{
    public static class InternalLoggingExtension
    {
        public static ILoggingBuilder AddInternalLogger(
            this ILoggingBuilder builder)
        {
            builder.AddConfiguration();

            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<ILoggerProvider, InternalLoggingProvider>());

            LoggerProviderOptions.RegisterProviderOptions
                <InternalLoggingConfiguration, InternalLoggingProvider>(builder.Services);

            return builder;
        }

        public static ILoggingBuilder AddInternalLogger(
            this ILoggingBuilder builder,
            Action<InternalLoggingConfiguration> configure)
        {
            builder.AddInternalLogger();
            builder.Services.Configure(configure);

            return builder;
        }
    }

   
}
