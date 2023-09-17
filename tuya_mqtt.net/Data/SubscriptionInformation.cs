namespace tuya_mqtt.net.Data
{
    public class SubscriptionInformation:IEqualityComparer<SubscriptionInformation>
    {
        public string Topic { get; private set; }
        private readonly Func<SubscriptionEventArgs,Task>? _eventFunction;

        public SubscriptionInformation(string topic)
        {
            Topic = topic;
            _eventFunction = null;
        }
        public SubscriptionInformation(string topic, Func<SubscriptionEventArgs, Task> func)
        {
            Topic = topic;
            _eventFunction = func;
        }

        public bool Equals(SubscriptionInformation other)
        {
            return Equals(this, other); 
        }
        public bool Equals(SubscriptionInformation? x, SubscriptionInformation? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.Topic == y.Topic;
        }

        public int GetHashCode(SubscriptionInformation obj)
        {
            return obj.Topic.GetHashCode();
        }

        internal async Task CallActionAsync(SubscriptionKey key, string content)
        {
            if (_eventFunction != null)
            {
                await _eventFunction(new SubscriptionEventArgs(key, content));
            }
        }
    }
}
