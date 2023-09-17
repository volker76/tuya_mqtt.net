namespace tuya_mqtt.net.Data
{
    public class SubscriptionEventArgs
    {
        public SubscriptionKey SubscriptionKey { get; set; } 
        public string Value { get; set; }

        public SubscriptionEventArgs(SubscriptionKey key, string value)
        {
            SubscriptionKey = key;
            Value = value;
        }

    }
}
