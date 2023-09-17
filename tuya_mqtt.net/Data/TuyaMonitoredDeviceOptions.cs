namespace tuya_mqtt.net.Data
{
    public class TuyaMonitoredDeviceOptions
    {
        public Dictionary<string, TuyaExtendedDeviceInformation> monitoredDUT { get; set; } =
            new Dictionary<string, TuyaExtendedDeviceInformation>();

    }
}
