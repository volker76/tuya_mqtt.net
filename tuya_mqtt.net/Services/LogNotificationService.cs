using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using tuya_mqtt.net.Data;

namespace tuya_mqtt.net.Services
{
    public class LogNotificationService
    {
        private readonly ILogger _logger;
        private readonly LogNotificationServiceOptions _options;
        private readonly List<LogItem> _messages;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1); 
        public LogNotificationService(ILogger<LogNotificationService> logger, IOptions<LogNotificationServiceOptions> options)
        {
            _logger = logger;
            _options = options.Value;
            _messages = new List<LogItem>(_options.MaxItemStorage);
        }

        public void SendLogItem(LogItem logItem)
        {
            _semaphore.Wait();
            try
            {
                if (_messages.Count == _options.MaxItemStorage)
                {
                    _messages.RemoveAt(0);
                }

                _messages.Add(logItem);

                OnLogAdded?.Invoke(this, logItem);
            }
            finally { _semaphore.Release();}
        }

        public event EventHandler<LogItem>? OnLogAdded;

        public IEnumerable<LogItem> GetLogItems()
        {
            _semaphore.Wait();
            try
            {
                foreach (var item in _messages)
                {
                    yield return item;
                }
            }
            finally { _semaphore.Release(); }
        }

        public Stream GetLogFile()
        {

            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            foreach (var item in _messages)
            {
#warning need TS to be converted to UTC
                var dt = item.TimeStamp.ToString("u");
                writer.Write(dt);
                writer.Write(" ");
                writer.Write(FormatLogLevel(item.LogLevel));
                writer.Write(" ");
                writer.Write(FormatMessage(item.Message));
                writer.WriteLine();
                if (item.Exception != null)
                {
                    var ex = item.Exception;
                    int indent = dt.Length + 1;
                    while (ex != null)
                    {
                        writer.WriteLine(WriterException(indent, ex));
                        if (ex.InnerException != null)
                        {
                            ex = ex.InnerException;
                            indent += 2;
                        }
                        else 
                        { ex = null; }
                    }
                }
            }
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        private string WriterException(int indent, Exception ex)
        {
            string output = string.Empty;
            for (int i = 0; i < indent; i++)
            {
                output += " ";
            }

            output += ex.GetType().FullName;

            output += Environment.NewLine;

            for (int i = 0; i < indent; i++)
            {
                output += " ";
            }

            if (!string.IsNullOrEmpty(ex.Message))
            {
                output += ex.Message;
            }

            output += Environment.NewLine;

            for (int i = 0; i < indent; i++)
            {
                output += " ";
            }

            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                output += ex.StackTrace;
            }

            return output;
        }

        private string FormatMessage(string itemMessage)
        {
            return itemMessage.Replace('\n', ' ')
                              .Replace('\r',' ');
        }

        private string FormatLogLevel(LogLevel itemLogLevel)
        {
            switch (itemLogLevel)
            {
                case LogLevel.Critical:
                    return "CRIT ";
                case LogLevel.Warning:
                    return "WARN ";
                case LogLevel.Error:
                    return "ERROR";
                case LogLevel.Trace:
                    return "TRACE";
                case LogLevel.Information:
                    return "INFO ";
                case LogLevel.Debug:
                    return "DEBUG";
                case LogLevel.None:
                    return "NONE ";
            }

            return "     ";
        }
    }
}
