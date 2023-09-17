using Castle.Components.DictionaryAdapter;

namespace tuya_mqtt.net.Data
{
    public class SubscriptionKey :Tuple<int,string>
    {
        public string ID
        {
            get => base.Item2;
        }
        public int DpNumber
        {
            get => base.Item1;
        }

        public SubscriptionKey(string id, int dp):base(dp,id)
        {
        }

    }
}
