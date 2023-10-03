using Newtonsoft.Json.Linq;

namespace tuya_mqtt.net.Data
{
    // ReSharper disable once InconsistentNaming
    public class DP
    {
        private readonly int _dpNo;
        private readonly string _valueString;

        public DP(int dpNo, string value)
        {
            this._dpNo = dpNo;
            this._valueString = value;
        }

        // ReSharper disable once InconsistentNaming
        public static List<DP> ParseCloudJSON(string json)
        {
            var list = new List<DP>();
            try
            {
                JObject bigObject = JObject.Parse(json);
                var properties = bigObject["properties"];
                if (properties != null)
                {
                    foreach (var d in properties.Children())
                    {
                        var name = d["dp_id"];
                        var value = d["value"];
               
                        if (value != null && name != null)
                        {
                            int id;
                            if (int.TryParse(name.ToString(), out id))
                            {
                                list.Add(new DP(id, value.ToString()));
                            }

                        }
                    }
                }

                return list;
            }
            catch (Exception ex)
            {
                throw new Exception($"error while parsing tuya device response '{json}'", ex);
            }
        }

        // ReSharper disable once InconsistentNaming
        public static List<DP> ParseLocalJSON(string json)
        {

            var list = new List<DP>();
            try
            {
                JObject bigObject = JObject.Parse(json);
                var dps = bigObject["dps"];
                if (dps != null)
                {
                    foreach (var d in dps.Children())
                    {
                        var name = ((JProperty)d).Name;
                        var value = d;
                        int id;
                        if (int.TryParse(name, out id))
                        {
                            list.Add(new DP(id, value.ToObject<string>()!));
                        }
                    }
                }
                return list;
            }
            catch (Exception ex)
            {
                throw new Exception($"error while parsing tuya device response '{json}'", ex);
            }
        }

        // ReSharper disable once InconsistentNaming
        public static string FindPropertyByDP(string json, int dpNumber)
        {
            try
            {
                JObject bigObject = JObject.Parse(json);
                var properties = bigObject["properties"];
                if (properties != null)
                {
                    foreach (var d in properties.Children())
                    {
                        var name = d["dp_id"]!.ToString();
                        int dp;
                        if (int.TryParse(name, out dp))
                        {
                            if (dp == dpNumber)
                                return d["code"]!.ToString();
                        }
                    }
                }

                throw new Exception($"requested data point (DP) does not exists in '{json}'");
            }
            catch (Exception ex)
            {
                throw new Exception($"error while parsing tuya device response '{json}'", ex);
            }
        }

        // ReSharper disable once InconsistentNaming
        public int DPNumber { get { return _dpNo; } }
        public string Value { get { return _valueString; } }

    }
}
