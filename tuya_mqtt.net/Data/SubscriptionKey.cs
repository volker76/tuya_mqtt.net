using Castle.Components.DictionaryAdapter;

namespace tuya_mqtt.net.Data
{
    public class SubscriptionKey :Tuple<byte,string>
    {
        public string ID
        {
            get => base.Item2;
        }
        public byte DpNumber
        {
            get => base.Item1;
        }

        public SubscriptionKey(string id, byte dp):base(dp,id)
        {
        }

    }
}
