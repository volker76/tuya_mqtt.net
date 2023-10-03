namespace tuya_mqtt.net.Data
{
    public class GlobalOptions
    {
        public bool MtqqReconnect { get; set; } = true;
        public int ExceptionsHealthDegraded { get; set; } = 10; //>10 Exceptions per minute = degraded
        public int ExceptionsUnhealthy { get; set; } = 40; //>40 Exceptions per minute = unhealthy

        public TimeSpan MqttDisconnectUnhealthy { get; set; } = TimeSpan.FromMinutes(10); //10 minutes disconnect = unhealthy
    }
}
