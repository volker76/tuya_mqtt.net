using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace tuya_mqtt.net.Data
{
    public class DP
    {
        private readonly int _dp_no;
        private readonly string _valueString;

        public DP(int dpNo, string value)
        {
            this._dp_no = dpNo;
            this._valueString = value;
        }

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
                        var Name = d["dp_id"].ToString();
                        var Value = d["value"].ToString();
               
                        if (Value != null && Name != null)
                        {
                            int id;
                            if (int.TryParse(Name, out id))
                            {
                                list.Add(new DP(id, Value));
                            }

                        }
                    }
                }

                return list;
            }
            catch (Exception ex)
            {
                throw new Exception($"error while parsing tuya device response {json}", ex);
            }
        }

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
                        var Name = ((Newtonsoft.Json.Linq.JProperty)d).Name;
                        var Value = d.ToObject<string>();
                        if (Value != null && Name != null)
                        {
                            int id;
                            if (int.TryParse(Name, out id))
                            {
                                list.Add(new DP(id, Value));
                            }

                        }
                    }
                }
                return list;
            }
            catch (Exception ex)
            {
                throw new Exception($"error while parsing tuya device response {json}", ex);
            }
        }


        public int DPNumber { get { return _dp_no; } }
        public string Value { get { return _valueString; } }

    }
}
