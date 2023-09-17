using System.Collections.Concurrent;
using com.clusterrr.TuyaNet;
using tuya_mqtt.net.Data;
using System.Net;
using Awesome.Net.WritableOptions;
using System.Collections.ObjectModel;

namespace tuya_mqtt.net.Services
{
    public class TuyaConnectedDeviceService
    {
        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<string, TuyaExtendedDeviceInformation> _monitoredDevices;
        private const int TimerPollInterval = 500;
        private DateTime _lastCleaned = DateTime.MinValue;

        private Timer? _timer;
        private readonly IWritableOptions<TuyaMonitoredDeviceOptions> _tuyaDevices;
        private readonly TimedDictionary<string, List<DP>> _monitoredData;

        public event EventHandler? OnDataUpdate;
        public TuyaConnectedDeviceService(IServiceProvider serviceProvider, ILogger<TuyaConnectedDeviceService> logger, IWritableOptions<TuyaMonitoredDeviceOptions> tuyaDevices)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _tuyaDevices = tuyaDevices;
            _monitoredDevices = new ConcurrentDictionary<string, TuyaExtendedDeviceInformation>(_tuyaDevices.Value.monitoredDUT);
            _monitoredData = new TimedDictionary<string, List<DP>>(TimeSpan.FromSeconds(60));
            _monitoredData.OnListUpdated += monitoredData_OnListUpdated;
        }

        private void monitoredData_OnListUpdated(object? sender, List<DP>? e)
        {
            OnDataUpdate?.Invoke(this, EventArgs.Empty);
        }

        public ReadOnlyDictionary<string, TuyaExtendedDeviceInformation> GetMonitoredDevices()
        {
            ReadOnlyDictionary<string, TuyaExtendedDeviceInformation> d =
                new ReadOnlyDictionary<string, TuyaExtendedDeviceInformation>(_monitoredDevices);

            return d;
        }

        public ReadOnlyDictionary<string, List<DP>?>? GetMonitoredData()
        {
            return _monitoredData?.ToReadonlyDictionary();
        }

        public async Task StartAsync()
        {
            if (_timer != null)
            {
                await _timer.DisposeAsync();
                _timer = null;
                await Task.Delay(1000);
            }
            _timer = new Timer(TimerFunction, null, 0, TimerPollInterval);
        }

        public async Task StopAsync()
        {
            if (_timer != null)
            {
                await _timer.DisposeAsync();
                _timer = null;
                await Task.Delay(1000);
            }
        }

        private void TimerFunction(object? state)
        {
            var tuyaservice = TuyaCommunicationHandler;
            var mqttservice = MqttClient;

            if (tuyaservice != null && mqttservice != null)
            {
                foreach (var device in _monitoredDevices.Values)
                {
                    try
                    {
                        if (device.IsTriggerDue()) 
                        {
                          
                            Task.Run(async () =>
                            {
                                try
                                {
                                    List<DP> result = await tuyaservice.GetDPAsync(device);
                                    _monitoredData?.Set(device.ID, new List<DP>(result),DataTimeout(device.PollingInterval));

                                    var topics = await mqttservice.PublishAsync(device.ID, device.Name, result);

                                    foreach (var topicItem in topics)
                                    {
                                        var sub_key = new SubscriptionKey(device.ID, topicItem.Item1.DPNumber);
                                        var sub_info = new SubscriptionInformation(topicItem.Item2 + "/command", SetDPAsync);
                                        if (MqttSubscription?.IsSubscribed(sub_key, sub_info) == false)
                                        {
                                            MqttSubscription?.MakeSubscription(sub_key, sub_info);
                                        }
                                    }

                                }
                                finally
                                {
                                    device.SetTrigger();
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"error when executing polling of device {device.Address} {device.Name}");
                    }

                }
            }
            else
            {
                if (mqttservice == null) 
                    _logger.LogDebug($"mqttservice is null");
                if (tuyaservice == null)
                    _logger.LogDebug($"tuyaservice is null");
            }

        }

        private async Task SetDPAsync(SubscriptionEventArgs key)
        {
            var tuyaservice = TuyaCommunicationHandler;
            var mqttservice = MqttClient;

            if (tuyaservice != null && mqttservice != null)
            {
                if (_monitoredDevices.TryGetValue(key.SubscriptionKey.ID, out var device))
                {
                    int repeat = 5;
                    while (repeat > 0)
                    {
                        try
                        {
                            var result = await tuyaservice.SetDPAsync(device, key.SubscriptionKey.DpNumber, key.Value);

                            _monitoredData?.Set(device.ID, new List<DP>(result), DataTimeout(device.PollingInterval));

                            var topics = await mqttservice.PublishAsync(device.ID, device.Name, result);
                            break;
                        }
                        catch (TimeoutException e)
                        {
                            repeat -= 1;
                            if (repeat > 0) _logger.LogWarning(e,$"Timeout set tuya DP at {device.Name}");
                            else _logger.LogError(e,$"Timeout set tuya DP at {device.Name}");
                        }
                        catch (Exception e)
                        {
                            repeat -= 1;
                            _logger.LogError(e, $"cannot set tuya DP {device.Name}:{key.SubscriptionKey.DpNumber}");
                        }

                        await Task.Delay(TimeSpan.FromSeconds(0.1));
                    }
                }
            }
            else
            {
                if (mqttservice == null)
                    _logger.LogDebug($"mqttservice is null");
                if (tuyaservice == null)
                    _logger.LogDebug($"tuyaservice is null");
            }
        }

        private TimeSpan DataTimeout(TimeSpan devicePollingInterval)
        {
            return 3 * devicePollingInterval;
        }

        

        private TuyaCommunicatorService? TuyaCommunicationHandler
        {
            get
            {
                var service = _serviceProvider.GetRequiredService<TuyaCommunicatorService>();
                return service;

            }
        }
        private MqttClientService? MqttClient
        {
            get
            {
                var service = _serviceProvider.GetRequiredService<MqttClientService>();
                return service;

            }
        }

        private MqttSubscriptionService? MqttSubscription
        {
            get
            {
                var service = _serviceProvider.GetRequiredService<MqttSubscriptionService>();
                return service;

            }
        }

        public bool TimerRunning
        {
            get
            {
                if (_timer != null)
                {
                    return true;
                }
                else { return false; }
            }
        }

        public int TimerInterval => TimerPollInterval;

        private void SaveOptions()
        {
            _tuyaDevices.Update((opt) =>
            {
                opt.monitoredDUT = _monitoredDevices.ToDictionary(kvp => kvp.Key,
                    kvp => kvp.Value,
                    _monitoredDevices.Comparer);
            });
        }
        public void AddDevice(TuyaDeviceInformation device, string name, TimeSpan pollingInterval)
        {
            var dev = new TuyaExtendedDeviceInformation(device, name, pollingInterval);
            _monitoredDevices.AddOrUpdate(dev.ID, dev, (key, current) => dev);
            SaveOptions();
        }

        public void RemoveDevice(TuyaDeviceInformation device)
        {
            SaveOptions();
        }

        public void UpdateIp(TuyaDeviceScanInfo scannedDevice)
        {
            TuyaExtendedDeviceInformation? info;
            if (_monitoredDevices.TryGetValue(scannedDevice.GwId, out info))
            {
                if (!IPAddress.TryParse(info.Address, out var _))
                {
                    //this device is not set based on IP
                    return;
                }

                if (info.Address == scannedDevice.IP)
                {
                    //the same, we can abort this here
                    return;
                }

                TuyaExtendedDeviceInformation newInfo = info;
                info.Address = scannedDevice.IP;
                bool success = _monitoredDevices.TryUpdate(scannedDevice.GwId, newInfo, info);
                if (success)
                {
                    _logger.LogInformation($"updated {scannedDevice.GwId} from ip {info.Address} to {info.Address}");
                }
            }
        }


        public TuyaExtendedDeviceInformation? GetDevice(string id)
        {
            if (_monitoredDevices.TryGetValue(id, out var item))
            {
                return item;
            }
            return null;
        }
    }
}
