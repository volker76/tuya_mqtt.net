using Newtonsoft.Json.Linq;
using System.Reflection.Metadata.Ecma335;

namespace tuya_mqtt.net.Data
{
    // ReSharper disable once InconsistentNaming
    public class DP
    {
        private readonly byte _dpNo;
        private readonly string _valueString;

        public DP(byte dpNo, string value)
        {
            this._dpNo = dpNo;
            this._valueString = value;
        }

        // ReSharper disable once InconsistentNaming
        public static List<DP> ParseCloudJSON(string json, List<byte>? DPList = null)
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
                            byte id;
                            if (byte.TryParse(name.ToString(), out id))
                            {
                                if (IdInList(id, DPList))
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

        private static bool IdInList(byte id, List<byte>? dPList)
        {
            if (dPList == null) return true; //default we take it all
            else
            {
                if (dPList.Count == 0) return true; //empty list means we take all
                else
                {
                    if (dPList.Contains(id)) return true;
                }
            }
            return false;
        }

        // ReSharper disable once InconsistentNaming
        public static List<DP> ParseLocalJSON(string json, List<byte>? DPList = null)
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
                        byte id;
                        if (byte.TryParse(name, out id))
                        {
                            if (IdInList(id, DPList))
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
        public static string FindPropertyByDP(string json, byte dpNumber)
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
        public byte DPNumber { get { return _dpNo; } }
        public string Value { get { return _valueString; } }

    }
}
