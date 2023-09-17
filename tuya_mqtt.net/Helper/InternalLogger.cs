using tuya_mqtt.net.Data;
using tuya_mqtt.net.Services;

namespace tuya_mqtt.net.Helper
{
    public sealed class InternalLogger : ILogger
    {
        private readonly string _name;
        private readonly IServiceProvider _serviceProvider;

        public InternalLogger(
            string name, IServiceProvider serviceProvider)
        {
            _name = name;
            _serviceProvider = serviceProvider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => default!;

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (exception != null) 
            { }
            var service = _serviceProvider.GetService(typeof(LogNotificationService)) as LogNotificationService;
            service?.SendLogItem(new LogItem(DateTime.Now, logLevel, message, exception));

        }
    }
}