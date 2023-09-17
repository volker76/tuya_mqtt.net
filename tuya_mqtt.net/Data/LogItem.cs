namespace tuya_mqtt.net.Data
{
    public class LogItem
    {
        public LogItem(DateTime ts, LogLevel level, string message, Exception? ex = null)
        {
            Message = message;
            LogLevel = level;
            Exception = ex;
            TimeStamp = ts;
        }
        public string Message { get; set; }
        public LogLevel LogLevel { get; set; }
        public Exception? Exception { get; set; }
        public DateTime TimeStamp { get; set; }
    }
}
