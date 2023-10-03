using com.clusterrr.TuyaNet;
using Newtonsoft.Json;

namespace tuya_mqtt.net.Data
{
    public class TuyaDeviceInformation
    {
        public enum DeviceType
        {
            LocalTuya = 1,
            Cloud =10,

        }
        public TuyaDeviceInformation(TuyaDeviceInformation device)
        {
            this.ID = device.ID;
            this.Address = device.Address;
            this.Key = device.Key;  
            this.ProtocolVersion = device.ProtocolVersion;
            this.CommunicationType = device.CommunicationType;
        }

        public TuyaDeviceInformation()
        {
       
        }

        public string ID { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public TuyaProtocolVersion ProtocolVersion { get; set; } = TuyaProtocolVersion.V33;
        public DeviceType CommunicationType { get; set; } = DeviceType.LocalTuya;

    }

    public class TuyaExtendedDeviceInformation : TuyaDeviceInformation
    {
        private string _name = String.Empty;
        private bool _locked = false;

        public TuyaExtendedDeviceInformation()
        {

        }
        public TuyaExtendedDeviceInformation(TuyaDeviceInformation device, string name, TimeSpan pollingInterval):base(device)
        {
            _name = name;
            PollingInterval = pollingInterval;
            LastTriggered = DateTime.MinValue;
        }

        [JsonIgnore]
        public bool hasName
        {
            get
            {
                return !string.IsNullOrEmpty(_name);
            }
        }
        public string Name
        {
            get
            {
                if (!string.IsNullOrEmpty(_name))
                {
                    return _name;
                }
                else
                {
                    return ID;
                }
            }
            set
            {
                if (value != ID)
                    _name = value;
            }
        }

        public TimeSpan PollingInterval { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public DateTime LastTriggered { get; set; }

        public bool IsTriggerDue()
        {
            lock (this)
            {
                var due = DateTime.Now > LastTriggered + PollingInterval;
                if (due && !_locked)
                {
                    _locked = true;
                    return true;
                }

                return false;
            }
        }

        public void SetTrigger()
        {
            lock (this)
            {
                LastTriggered = DateTime.Now;
                _locked = false;
            }
        }
    }
}