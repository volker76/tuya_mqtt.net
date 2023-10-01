using System.Diagnostics;
using Microsoft.Extensions.Options;
using tuya_mqtt.net.Data;
using com.clusterrr.TuyaNet;
using System.Security.Cryptography;
using MudBlazor;
using System.Threading.Tasks;
using System.Threading;

namespace tuya_mqtt.net.Services
{
    public class TuyaCommunicatorService : IDisposable
    {
        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TuyaScanner? _networkScanner;
        private readonly TimedDictionary<string, TuyaDeviceScanInfo> _tuyaScanDevices;
        private readonly Timer _startupDelay;
        private readonly TimeSpan TuyaDeviceExpired = TimeSpan.FromSeconds(30); //30 second constant
        private const int TuyaTimeout = 2000; //2000ms timeout

        public event EventHandler<TuyaDeviceScanInfo>? OnTuyaScannerUpdate;

        private readonly IOptionsMonitor<TuyaCommunicatorOptions> _options;
        private TuyaCommunicatorOptions Options => _options.CurrentValue;

        private readonly IOptionsMonitor<GlobalOptions> _globaloptions;
        private GlobalOptions GlobalOptions => _globaloptions.CurrentValue;

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

        public TuyaCommunicatorService(IServiceProvider sp, ILogger<TuyaCommunicatorService> logger, IOptionsMonitor<TuyaCommunicatorOptions> options, IOptionsMonitor<GlobalOptions> globalOptions)
        {
            _logger = logger;
            _serviceProvider = sp;
            _options = options;
            _globaloptions = globalOptions;

            _tuyaScanDevices = new TimedDictionary<string, TuyaDeviceScanInfo>(TuyaDeviceExpired);
            _tuyaScanDevices.OnListUpdated += ScanDevicesListUpdated;
            
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
            
            _logger.LogDebug($"TestConnect to {device.Address} ID:{device.ID} Key:{device.Key}");
            if (string.IsNullOrEmpty(device.ID))
                throw new ArgumentOutOfRangeException(nameof(device.ID),"ID cannot be empty");
            if (string.IsNullOrEmpty(device.Key))
                throw new ArgumentOutOfRangeException(nameof(device.Key), "Key cannot be empty");
            try
            {
                var dev = new TuyaDevice(device.Address, device.Key.Trim(), device.ID.Trim(), device.ProtocolVersion);
                var json_in=dev.FillJson("");
                byte[] request = dev.EncodeRequest(TuyaCommand.DP_QUERY, json_in);


                var t = dev.SendAsync(request);

                if (await Task.WhenAny(t, Task.Delay(TuyaTimeout)) == t) // timeout 
                {
                    byte[] encryptedResponse = t.Result;
                    TuyaLocalResponse response = dev.DecodeResponse(encryptedResponse);
                    string json = response.JSON;
                    _logger.LogDebug($"TestConnect to {device.Address} returned data set {json}");
                    var list = DP.ParseJSON(json);

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

        // ReSharper disable once InconsistentNaming
        public async Task<List<DP>> GetDPAsync(TuyaExtendedDeviceInformation device)
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
                
                if (await Task.WhenAny(t, Task.Delay(TuyaTimeout)) == t) // timeout 
                {
                    byte[] encryptedResponse = t.Result;
                    TuyaLocalResponse response = dev.DecodeResponse(encryptedResponse);
                    string json = response.JSON;
                    _logger.LogInformation($"Get DPs from {device.Address}'{device.ID}' returned data set {json}");
                    var list = DP.ParseJSON(json);

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
                throw new TimeoutException($"Timeout when reading device {device.Name} {e.Message}", e);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        internal async Task<List<DP>> SetDPAsync(TuyaExtendedDeviceInformation device, int dpNumber, string value)
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
                    var list = DP.ParseJSON(json);

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
                throw new TimeoutException($"Timeout when setting {device.Name}:{dpNumber} to '{value}' {e.Message}",e);
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
    }
}
