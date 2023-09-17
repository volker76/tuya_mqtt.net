using System.Reflection;

namespace tuya_mqtt.net.Data
{
    public class MqttClientOptions
    {
        public string MqttHost { get; set; } = String.Empty;
        public int MqttPort { get; set; } = -1;

        public bool MqttAuthentication { get; set; } = false;
        public string MqttUser { get; set; } = String.Empty;

        public string MqttPassword { get; set; } = String.Empty;

        public string MqttTopic { get; set; } = String.Empty;

        public bool MqttV5 { get; set; } = false;
        public bool MqttTls { get; set; } = false;
        public bool MqttNoFragmentation { get; set; } = false;


        public bool Equals(MqttClientOptions? x)
        {
            return  x?.MqttHost == MqttHost &&
                    x?.MqttPort == MqttPort &&
                    x?.MqttUser == MqttUser &&
                    x?.MqttTopic == MqttTopic &&
                    x?.MqttPassword == MqttPassword &&
                    x?.MqttV5 == MqttV5 &&
                    x?.MqttTls == MqttTls && 
                    x?.MqttAuthentication == MqttAuthentication &&
                    x?.MqttNoFragmentation == MqttNoFragmentation;
        }

        public override string ToString()
        {
            Type type = GetType();
            var collectionString = type.Name + " (";
            foreach (PropertyInfo prop in type.GetProperties())
            {
                collectionString += $" {prop.Name}:{prop.GetValue(this)}";
            }

            collectionString += " )";
            return collectionString;
        }

    }
}
