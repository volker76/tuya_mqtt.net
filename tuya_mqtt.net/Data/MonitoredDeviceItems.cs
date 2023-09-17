namespace tuya_mqtt.net.Data
{
    public class MonitoredDeviceItems
    {
        public string Title { get; set; }

        // ReSharper disable once InconsistentNaming
        public string ID { get; set; }

        public string Icon { get; set; }

        public string? Value { get; set; }

        public string? Address { get; set; }

        public bool IsExpanded { get; set; }

        public HashSet<MonitoredDeviceItems> SubItems { get; set; }

        public MonitoredDeviceItems(string title, string icon, string id, string? addr = null, string? value = null)
        {
            Title = title;
            Icon = icon;
            Value = value;
            Address = addr;
            ID = id;
            SubItems = new HashSet<MonitoredDeviceItems>();
        }
    }
}
