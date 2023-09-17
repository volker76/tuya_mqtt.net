using System.Collections.Concurrent;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace tuya_mqtt.net.Helper
{
    public  class InternalLoggingConfiguration 
    {
        
    }
    public sealed class InternalLoggingProvider : ILoggerProvider
    {
      
        private readonly ConcurrentDictionary<string, InternalLogger> _loggers =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly IServiceProvider _serviceProvider;

        public InternalLoggingProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
        public void Dispose()
        {
            _loggers.Clear();
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new InternalLogger(name,_serviceProvider));
        }
    }
}