using com.clusterrr.TuyaNet;
using System.Reflection;

namespace tuya_mqtt.net.Data
{
    public class TuyaCommunicatorOptions
    {
        public TuyaCommunicatorOptions() { }

        public string TuyaAPIAccessID { get; set; } = String.Empty;

        public string TuyaAPISecret { get; set; } = String.Empty;

        public TuyaApi.Region TuyaAPIRegion { get; set; } = TuyaApi.Region.WesternEurope;

        public bool Equals(TuyaCommunicatorOptions? x)
        {
            return x?.TuyaAPIAccessID == TuyaAPIAccessID &&
                   x?.TuyaAPISecret == TuyaAPISecret &&
                   x?.TuyaAPIRegion.GetHashCode() == TuyaAPIRegion.GetHashCode();
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
