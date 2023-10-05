using System.Diagnostics;
using Microsoft.Extensions.Options;
using tuya_mqtt.net.Data;
using com.clusterrr.TuyaNet;
using System.Security.Cryptography;
using JetBrains.Annotations;
using MudBlazor;
using Newtonsoft.Json.Linq;
using static tuya_mqtt.net.Data.TuyaDeviceInformation;
using System.Text.Json.Nodes;
// ReSharper disable InconsistentNaming

namespace tuya_mqtt.net.Services
{
    public class TuyaCommunicatorService : IDisposable
    {
        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TuyaScanner? _networkScanner;
        private readonly TimedDictionary<string, TuyaDeviceScanInfo> _tuyaScanDevices;

        private readonly TimedDictionary<Tuple<string, int>, string> _tuyaPropertyCache;

        private readonly Timer _startupDelay;

        
        private readonly TimeSpan TuyaDeviceExpired = TimeSpan.FromSeconds(30); //30 second constant
        private const int TuyaTimeout = 2000; //2000ms timeout
        
        private readonly TimeSpan TuyaPropertyCacheExpiration = TimeSpan.FromHours(4);

        public event EventHandler<TuyaDeviceScanInfo>? OnTuyaScannerUpdate;

        private readonly IOptionsMonitor<TuyaCommunicatorOptions> _tuyaoptions;
        private TuyaCommunicatorOptions Options => _tuyaoptions.CurrentValue;

        private readonly IOptionsMonitor<GlobalOptions> _globaloptions;
        [UsedImplicitly] private GlobalOptions GlobalOptions => _globaloptions.CurrentValue;

        private TuyaConnectedDeviceService? ConnectedDevices
        {
            get
            {
                try
                {
                    var service = _serviceProvider.GetRequiredService<TuyaConnectedDeviceService>();
                    return service;
                }
                catch { return null; }
            }
        }

        public IEnumerable<TuyaApi.Region> Regions
        {
            get
            {
                List< TuyaApi.Region > regions = new List<TuyaApi.Region>();
                foreach (TuyaApi.Region r in Enum.GetValues(typeof(TuyaApi.Region)))
                {
                    regions.Add(r);
                }

                return regions;
            }
        }

        public bool TuyaApiConfigured
        {
            get
            {
                // ReSharper disable once ArrangeRedundantParentheses
                return (!string.IsNullOrEmpty(Options.TuyaAPIAccessID) && !string.IsNullOrEmpty(Options.TuyaAPISecret));
            }
        }

        public TuyaCommunicatorService(IServiceProvider sp, ILogger<TuyaCommunicatorService> logger, IOptionsMonitor<TuyaCommunicatorOptions> options, IOptionsMonitor<GlobalOptions> globalOptions)
        {
            _logger = logger;
            _serviceProvider = sp;
            _tuyaoptions = options;
            _globaloptions = globalOptions;

            _tuyaScanDevices = new TimedDictionary<string, TuyaDeviceScanInfo>(TuyaDeviceExpired);
            _tuyaScanDevices.OnListUpdated += ScanDevicesListUpdated;

            _tuyaPropertyCache = new TimedDictionary<Tuple<string, int>, string>(TuyaPropertyCacheExpiration);

            _networkScanner = new TuyaScanner();
            _networkScanner.OnDeviceInfoReceived += Scanner_OnDeviceInfoReceived;
            _networkScanner.Start();

            _startupDelay = new Timer(StartUpDelayFireAsync, null, TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(100));

            _logger.LogDebug("constructor finished");
        }

        private void ScanDevicesListUpdated(object? sender, TuyaDeviceScanInfo? e)
        {
            Debug.Assert(e != null, nameof(e) + " != null");
            OnTuyaScannerUpdate?.Invoke(this, e);
        }

        private void Scanner_OnDeviceInfoReceived(object? sender, TuyaDeviceScanInfo e)
        {
            if (_tuyaScanDevices.ContainsKey(e.IP))
            {
                _logger.LogDebug($"TuyaScanner: device updated IP:{e.IP}");
            }
            else
            {
                _logger.LogInformation($"TuyaScanner: new device IP:{e.IP} ProdKey:{e.ProductKey}");
            }
            _tuyaScanDevices[e.IP] = e;
           
            if (OnTuyaScannerUpdate != null)
                OnTuyaScannerUpdate.Invoke(this, e);

            ConnectedDevices?.UpdateIp(e);
        }

        private void StartUpDelayFireAsync(object? state)
        {
            var obs = ConnectedDevices;
            if (obs != null)
            {
                _startupDelay.Change(Timeout.Infinite, Timeout.Infinite);
                Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(1000));
                    obs.StartAsync().AndForget();
                });

            }
        }
        

        public IEnumerable<KeyValuePair<string, TuyaDeviceScanInfo>> GetTuyaScanDevices()
        {
            return _tuyaScanDevices.OrderBy(a => a.Key);
        }

        public void Dispose()
        {
            var obs = ConnectedDevices;
            if (obs != null)
            {
                obs.StopAsync().AndForget();
            }

            // Dispose of unmanaged resources.
            if (_networkScanner != null)
            {
                _networkScanner.Stop();
                _tuyaScanDevices.Clear();
            }
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }


        public async Task<List<DP>> TestConnect(TuyaDeviceInformation device)
        {
            switch (device.CommunicationType)
            {
                case TuyaDeviceType.Cloud:
                    return await TestConnectCloud(device);
                case TuyaDeviceType.LocalTuya0A:
                    return await TestConnectLocal0A(device);
                case TuyaDeviceType.LocalTuya0D:
                    return await TestConnectLocal0D(device);
                default:
                    throw new Exception("device type unknown");
            }

        }
        private async Task<List<DP>> TestConnectCloud(TuyaDeviceInformation device)
        {
            _logger.LogDebug($"TestConnect to cloud for ID:{device.ID}");
            if (string.IsNullOrEmpty(device.ID))
                throw new ArgumentOutOfRangeException(nameof(device.ID), "ID cannot be empty");
            if (!TuyaApiConfigured)
                throw new InvalidOperationException("No cloud credentials configured");
            try
            {
                var api = new TuyaApi(region: Options.TuyaAPIRegion, accessId: Options.TuyaAPIAccessID, apiSecret: Options.TuyaAPISecret);

                Task<string> t = api.RequestAsync(TuyaApi.Method.GET,
                    $"v2.0/cloud/thing/{device.ID}/shadow/properties");

                if (await Task.WhenAny(t, Task.Delay(TuyaTimeout)) == t) // timeout 
                {
                    var json = t.Result;
                    var list = DP.ParseCloudJSON(json);

                    return list;
                }
                else
                {
                    throw new TimeoutException($"device: {device.Address} ID:{device.ID} did not respond in time");
                }

            }
            catch (Exception e)
            {
                _logger.LogError(e, $"error testing device {device.ID}");
                throw new Exception($"error testing device {device.ID}", e);
            }

        }
        private async Task<List<DP>> TestConnectLocal0A(TuyaDeviceInformation device)
        {
            _logger.LogDebug($"TestConnect (0A mode) to {device.Address} ID:{device.ID} Key:{device.Key}");
            if (string.IsNullOrEmpty(device.ID))
                throw new ArgumentOutOfRangeException(nameof(device.ID), "ID cannot be empty");
            if (string.IsNullOrEmpty(device.Key))
                throw new ArgumentOutOfRangeException(nameof(device.Key), "Key cannot be empty");
            try
            {
                var dev = new TuyaDevice(device.Address, device.Key.Trim(), device.ID.Trim(), device.ProtocolVersion);
                var jsonIn = dev.FillJson("");
                byte[] request = dev.EncodeRequest(TuyaCommand.DP_QUERY, jsonIn);


                var t = dev.SendAsync(request);

                if (await Task.WhenAny(t, Task.Delay(TuyaTimeout)) == t) // no timeout 
                {
                    byte[] encryptedResponse = t.Result;
                    TuyaLocalResponse response = dev.DecodeResponse(encryptedResponse);
                    string json = response.JSON;
                    _logger.LogDebug($"TestConnect to {device.Address} returned data set {json}");
                    var list = DP.ParseLocalJSON(json);

                    return list;
                }
                else
                {
                    throw new TimeoutException($"device: {device.Address} ID:{device.ID} did not respond in time");
                }
            }
            catch (CryptographicException e)
            {
                throw new Exception($"Key does not work for this device. {e.Message}");
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        private async Task<List<DP>> TestConnectLocal0D(TuyaDeviceInformation device)
        {
            _logger.LogDebug($"TestConnect (0D mode) to {device.Address} ID:{device.ID} Key:{device.Key}");
            if (string.IsNullOrEmpty(device.ID))
                throw new ArgumentOutOfRangeException(nameof(device.ID), "ID cannot be empty");
            if (string.IsNullOrEmpty(device.Key))
                throw new ArgumentOutOfRangeException(nameof(device.Key), "Key cannot be empty");
            try
            {
                var dev = new TuyaDevice(device.Address, device.Key.Trim(), device.ID.Trim(), device.ProtocolVersion);
                var jsonIn = dev.FillJson("{\"dps\":{\"1\":null}}", true, true, true, true);
                byte[] request = dev.EncodeRequest(TuyaCommand.CONTROL_NEW, jsonIn);

                var t = dev.SendAsync(request);

                if (await Task.WhenAny(t, Task.Delay(TuyaTimeout)) == t) // no timeout 
                {
                    byte[] encryptedResponse = t.Result;
                    TuyaLocalResponse response = dev.DecodeResponse(encryptedResponse);
                    string json = response.JSON;
                    _logger.LogDebug($"TestConnect to {device.Address} returned data set {json}");
                    var list = DP.ParseLocalJSON(json);

                    return list;
                }
                else
                {
                    throw new TimeoutException($"device: {device.Address} ID:{device.ID} did not respond in time");
                }
            }
            catch (CryptographicException e)
            {
                throw new Exception($"Key does not work for this device. {e.Message}");
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public async Task<List<DP>> GetDPAsync(TuyaDeviceInformation device, List<byte> DPList)
        {
            switch (device.CommunicationType)
            {
                case TuyaDeviceType.Cloud:
                    return await GetDPCloudAsync(device, DPList);
                case TuyaDeviceType.LocalTuya0A:
                    return await GetDPLocal0AAsync(device, DPList);
                case TuyaDeviceType.LocalTuya0D:
                    return await GetDPLocal0DAsync(device, DPList);
                default:
                    throw new Exception("invalid Communication type of device");
            }
        }

        private async Task<List<DP>> GetDPCloudAsync(TuyaDeviceInformation device, List<byte>? DPList)
        {
            _logger.LogDebug($"Poll cloud data from ID:{device.ID}");
            if (string.IsNullOrEmpty(device.ID))
                throw new ArgumentOutOfRangeException(nameof(device.ID), "ID cannot be empty");
            if (!TuyaApiConfigured)
                throw new InvalidOperationException("No cloud credentials configured");

            try
            {
                var api = new TuyaApi(region: Options.TuyaAPIRegion, accessId: Options.TuyaAPIAccessID, apiSecret: Options.TuyaAPISecret);

                Task<string> t = api.RequestAsync(TuyaApi.Method.GET,
                    $"v2.0/cloud/thing/{device.ID}/shadow/properties");

                if (await Task.WhenAny(t, Task.Delay(TuyaTimeout)) == t) // no timeout 
                {
                    var json = t.Result;
                    var list = DP.ParseCloudJSON(json, DPList);

                    return list;
                }
                else
                {
                    throw new TimeoutException($"device: ID:{device.ID} did not respond in time");
                }

            }
            catch (Exception e)
            {
                _logger.LogError(e, $"error retrieving device data ID={device.ID}");
                throw new Exception($"error retrieving device data ID={device.ID}", e);
            }

        }

        private async Task<List<DP>> GetDPLocal0AAsync(TuyaDeviceInformation device, List<byte>? DPList)
        {
            _logger.LogDebug($"Poll data from {device.Address} ID:{device.ID} Key:{device.Key}");
            if (string.IsNullOrEmpty(device.ID))
                throw new ArgumentOutOfRangeException(nameof(device.ID), "ID cannot be empty");
            if (string.IsNullOrEmpty(device.Key))
                throw new ArgumentOutOfRangeException(nameof(device.Key), "Key cannot be empty");
            try
            {
                var dev = new TuyaDevice(device.Address, device.Key.Trim(), device.ID.Trim(), device.ProtocolVersion);
                var jsonIn = dev.FillJson("");
                byte[] request = dev.EncodeRequest(TuyaCommand.DP_QUERY, jsonIn);

                var t = dev.SendAsync(request);
                
                if (await Task.WhenAny(t, Task.Delay(TuyaTimeout)) == t) // no timeout 
                {
                    byte[] encryptedResponse = t.Result;
                    TuyaLocalResponse response = dev.DecodeResponse(encryptedResponse);
                    string json = response.JSON;
                    _logger.LogInformation($"Get DPs from {device.Address}'{device.ID}' returned data set {json}");
                    var list = DP.ParseLocalJSON(json, DPList);

                    return list;
                }
                else
                {
                    throw new TimeoutException($"device: {device.Address} ID:{device.ID} did not respond in time");
                }
            }
            catch (CryptographicException e)
            {
                throw new Exception($"Key does not work for this device. {e.Message}");
            }
            catch (TimeoutException e)
            {
                throw new TimeoutException($"Timeout when reading device {device.ID} {e.Message}", e);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }
        private async Task<List<DP>> GetDPLocal0DAsync(TuyaDeviceInformation device, List<byte> DPList)
        {
            _logger.LogDebug($"Poll data from {device.Address} ID:{device.ID} Key:{device.Key}");
            if (string.IsNullOrEmpty(device.ID))
                throw new ArgumentOutOfRangeException(nameof(device.ID), "ID cannot be empty");
            if (string.IsNullOrEmpty(device.Key))
                throw new ArgumentOutOfRangeException(nameof(device.Key), "Key cannot be empty");
            if (DPList.Count==0)
                throw new ArgumentOutOfRangeException(nameof(DPList), "no monitored DPs specified");
            try
            {
                var dev = new TuyaDevice(device.Address, device.Key.Trim(), device.ID.Trim(), device.ProtocolVersion);

                string dparray = BuildDPJsonArray(DPList);
                var jsonIn = dev.FillJson("{\"dps\":{"+dparray+"}}", true, true, true, true);

                byte[] request = dev.EncodeRequest(TuyaCommand.CONTROL_NEW, jsonIn);

                var t = dev.SendAsync(request);

                if (await Task.WhenAny(t, Task.Delay(TuyaTimeout)) == t) // no timeout 
                {
                    byte[] encryptedResponse = t.Result;
                    TuyaLocalResponse response = dev.DecodeResponse(encryptedResponse);
                    string json = response.JSON;
                    _logger.LogInformation($"Get DPs from {device.Address}'{device.ID}' returned data set {json}");
                    var list = DP.ParseLocalJSON(json, DPList);

                    return list;
                }
                else
                {
                    throw new TimeoutException($"device: {device.Address} ID:{device.ID} did not respond in time");
                }
            }
            catch (CryptographicException e)
            {
                throw new Exception($"Key does not work for this device. {e.Message}");
            }
            catch (TimeoutException e)
            {
                throw new TimeoutException($"Timeout when reading device {device.ID} {e.Message}", e);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        private string BuildDPJsonArray(List<byte> DPList)
        {
            string result = string.Empty;
            foreach (byte dP in DPList)
            {
                result += $"\"{dP}\":null"+ ",";
            }
            return result.TrimEnd(','); //last "," to be removed
        }

        public async Task<List<DP>> SetDPAsync(TuyaDeviceInformation device, byte dpNumber, string value)
        {
            if (device.CommunicationType == TuyaDeviceInformation.TuyaDeviceType.Cloud)
            {
                await SetDPCloudAsync(device, dpNumber, value);
                await Task.Delay(100); 
                List<byte>? DPs = new List<byte>(){dpNumber};
                return await GetDPCloudAsync(device,DPs);
            }
            else
            {
                return await SetDPLocalAsync(device, dpNumber, value);
            }
        }

        private async Task SetDPCloudAsync(TuyaDeviceInformation device, byte dpNumber, string value)
        {
            _logger.LogDebug($"Set data to {device.Address} ID:{device.ID} Key:{device.Key}");
            if (string.IsNullOrEmpty(device.ID))
                throw new ArgumentOutOfRangeException(nameof(device.ID), "ID cannot be empty");
            if (!TuyaApiConfigured)
                throw new InvalidOperationException("No cloud credentials configured");

            try
            {
                var api = new TuyaApi(region: Options.TuyaAPIRegion, accessId: Options.TuyaAPIAccessID, apiSecret: Options.TuyaAPISecret);
                value = CorrectBool(value); // bool values True / False must be lower letters for JSON

                bool success = GetApiDeviceProperty(device.ID,dpNumber,out string prop);
                if (!success)
                {
                    prop = await RequestDevicePropertyAsync(api, device.ID, dpNumber);
                    SetApiDeviceProperty(device.ID, dpNumber, prop);
                }

                string body = $"{{ \"properties\": {{\"{prop}\":{value}}} }}";
                Task<string> t = api.RequestAsync(TuyaApi.Method.POST,
                    $"v2.0/cloud/thing/{device.ID}/shadow/properties/issue", body);

                if (await Task.WhenAny(t, Task.Delay(TuyaTimeout)) == t) // no timeout 
                {
                    return;
                }
                else
                {
                    throw new TimeoutException($"device: ID:{device.ID} did not respond in time");
                }

            }
            catch (Exception e)
            {
                _logger.LogError(e, $"error setting cloud ID={device.ID}:{dpNumber} to '{value}'");
                throw new Exception($"error setting cloud ID={device.ID}:{dpNumber} to '{value}'", e);
            }

        }

        private async Task<string> RequestDevicePropertyAsync(TuyaApi api, string deviceId, byte dpNumber)
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentOutOfRangeException(nameof(deviceId), "ID cannot be empty");
            if (!TuyaApiConfigured)
                throw new InvalidOperationException("No cloud credentials configured");

            Task<string> t = api.RequestAsync(TuyaApi.Method.GET,
                $"v2.0/cloud/thing/{deviceId}/shadow/properties");

            if (await Task.WhenAny(t, Task.Delay(TuyaTimeout)) == t) // timeout 
            {
                var json = t.Result;
                var prop = DP.FindPropertyByDP(json,dpNumber);

                return prop;
            }
            else
            {
                throw new TimeoutException($"device: ID:{deviceId} did not respond in time");
            }
        }

        private void SetApiDeviceProperty(string id, int dpNumber, string prop)
        {
            _tuyaPropertyCache.Set(new Tuple<string, int>(id, dpNumber),prop);
        }

        private bool GetApiDeviceProperty(string id, int dpNumber, out string prop)
        {
            prop = "";
            try
            {
                prop = _tuyaPropertyCache[new Tuple<string, int>(id, dpNumber)]!;
                return true;
            }
            catch
            {
                return false;
            }

        }

        private async Task<List<DP>> SetDPLocalAsync(TuyaDeviceInformation device, byte dpNumber, string value)
        {
            _logger.LogDebug($"Set data to {device.Address} ID:{device.ID} Key:{device.Key}");
            if (string.IsNullOrEmpty(device.ID))
                throw new ArgumentOutOfRangeException(nameof(device.ID), "ID cannot be empty");
            if (string.IsNullOrEmpty(device.Key))
                throw new ArgumentOutOfRangeException(nameof(device.Key), "Key cannot be empty");
            try
            {
                var dev = new TuyaDevice(device.Address, device.Key.Trim(), device.ID.Trim(), device.ProtocolVersion);
                value = CorrectBool(value); // bool values True / False must be lower letters for JSON
                var jsonIn = dev.FillJson("{\"dps\":{\""+dpNumber.ToString()+"\":"+value+"}}");


                byte[] request = dev.EncodeRequest(TuyaCommand.CONTROL, jsonIn);

                var t = dev.SendAsync(request); 
                if (await Task.WhenAny(t, Task.Delay(TuyaTimeout)) == t) // timeout 
                {
                    byte[] encryptedResponse = t.Result;
                    TuyaLocalResponse response = dev.DecodeResponse(encryptedResponse);
                    string json = response.JSON;
                    _logger.LogInformation($"Get DPs from {device.Address}'{device.ID}' returned data set {json}");
                    var list = DP.ParseLocalJSON(json);

                    return list;
                }
                else
                {
                    throw new TimeoutException($"device: {device.Address} ID:{device.ID} did not respond in time");
                }
            }
            catch (CryptographicException e)
            {
                throw new Exception($"Key does not work for this device. {e.Message}");
            }
            catch (TimeoutException e)
            {
                throw new TimeoutException($"Timeout when setting {device.ID}:{dpNumber} to '{value}' {e.Message}",e);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }

        }

        private string CorrectBool(string value)
        {
            if (value.Trim().ToLower() == "true")
                value = "true";
            if (value.Trim().ToLower() == "false")
                value = "false";

            return value;
        }

        public async Task TestCloudAPIAsync(string apiKey, string apiSecret, TuyaApi.Region region)
        {
            try
            { 
                var api = new TuyaApi(region: region, accessId: apiKey, apiSecret: apiSecret);
                
                //check if we can use it to make a request
                var json = await api.RequestAsync(TuyaApi.Method.GET,
                    $"/v2.0/cloud/space/child?only_sub=false");
                JObject jObject = JObject.Parse(json);
                var data = jObject["data"]; //there shall be a list named "data"
                if (data != null)
                {
                    if (!data.Any() )
                        throw new Exception("API credentials correct, but possibly wrong region.");
                }
                else
                    throw new Exception("API call did not return expected result.");

            }
            catch (Exception e)
            {
                _logger.LogError(e, "error testing TUYA API");
                throw new Exception("error testing TUYA API",e);
            }
        }

        public async Task<Tuple<bool, string, string>> GetDeviceDataFromApiAsync(string id)
        {
            string localKey=String.Empty;
            string deviceName=String.Empty;
            if (!TuyaApiConfigured)
            {
                _logger.LogInformation($"No Tuya API credentials configured to find information for ID:{id}.");
                return new Tuple<bool, string, string>(false, localKey, deviceName);
            }

            try
            {
                var api = new TuyaApi(region: Options.TuyaAPIRegion, accessId: Options.TuyaAPIAccessID, apiSecret: Options.TuyaAPISecret);

                var json = await api.RequestAsync(TuyaApi.Method.GET,
                    $"/v2.0/cloud/thing/{id}");
                JObject jObject = JObject.Parse(json);
                var localKeyToken = jObject["local_key"];
                if (localKeyToken != null)
                {
                    localKey = localKeyToken.Value<string>() ?? string.Empty;
                }
                var nameToken = jObject["custom_name"];
                if (nameToken != null)
                {
                    deviceName = nameToken.Value<string>() ?? string.Empty;
                }
                return new Tuple<bool, string, string>(true, localKey, deviceName);
                
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error requesting TuyaAPI for ID:{id}.");
                //don't throw any exception as the missing data can be entered manually also.
                return new Tuple<bool, string, string>(false, localKey, deviceName);
            }
        }

        public async Task<string> IdentifyDPs(TuyaDeviceInformation device)
        {
            if (TuyaApiConfigured)
            {
                return await IdentifyCloudDPs(device);
            }
            switch (device.CommunicationType)
            {
                case TuyaDeviceType.Cloud:
                    return await IdentifyCloudDPs(device);
                case TuyaDeviceType.LocalTuya0A:
                    return await IdentifyLocal0A_DPs(device);
                case TuyaDeviceType.LocalTuya0D:
                    return await IdentifyLocal0D_DPs(device);
                default:
                    throw new Exception("invalid Communication type of device");

            }
        }

        private async Task<string> IdentifyLocal0D_DPs(TuyaDeviceInformation device)
        {
            string result = string.Empty;
            byte increment = 10;
            for (int i = 1; i <= 255; i += increment)  //loop over all possible DPs
            {
                List<byte> testList = new List<byte>();
                int end = i + increment;
                if (end > 255) end = 255; //do not exceed >255
                for (int j = i; j < end; j++)
                {
                    if ((j is >= 1 and <= 30) || (j is >= 100 and <= 120)) // requesting a list of all DPs from 1 to 30 and 100 to 120
                        testList.Add((byte)j);
                }

                if (testList.Count > 0)
                {
                    for (int repeats = 0; repeats < 1; repeats++)
                    {
                        try
                        {
                            var monitorList = await GetDPLocal0DAsync(device, testList);
                            var list = BuildDPList(monitorList);
                            if (!string.IsNullOrEmpty(list))
                            {
                                result += list + " ";
                            }

                            break; //stop repeat cycle
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning(e, $"error checking for DPs '{DumpList(testList)}' on device {device.ID}");
                            await Task.Delay(100);
                            //try again
                        }
                    }
                }
            }
            
            return result.Trim(' ');
        }

        private string DumpList(List<byte> itemList)
        {
            string result = string.Empty;
            foreach (byte item in itemList)
            {
                result += $"{item} ";
            }
            return result.Trim();
        }

        private async Task<string> IdentifyLocal0A_DPs(TuyaDeviceInformation device)
        {
            var list = await GetDPLocal0AAsync(device, null);

            return BuildDPList(list);
        }

        private async Task<string> IdentifyCloudDPs(TuyaDeviceInformation device)
        {
            var list = await GetDPCloudAsync(device, null);

            return BuildDPList(list);
        }

        private string BuildDPList(List<DP> list)
        {
            string result = string.Empty;
            foreach (var dp in list)
            {
                result += $"{dp.DPNumber} ";
            }

            return result.Trim(' ');
        }
    }
}
