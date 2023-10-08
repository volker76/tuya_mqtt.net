using Abp.Threading;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Exceptions;
using tuya_mqtt.net.Data;
using tuya_mqtt.net.Helper;
using MqttClientOptions = tuya_mqtt.net.Data.MqttClientOptions;

namespace tuya_mqtt.net.Services
{
    public class MqttClientService : IDisposable
    {
        private readonly ILogger _logger;

        private readonly MqttFactory _mqttfactory;
        private IMqttClient? _mqttClient;
        private string _brokerVersion = String.Empty;
        private bool _autoReconnect;
        private readonly IMqttSubscriptionService _subscriptionService;
        private readonly MemoryCache _publishedDataCache = new MemoryCache(new MemoryCacheOptions());
        private readonly IOptionsMonitor<GlobalOptions> _globalOptions;
        private DateTime _lastHeartbeatTime;

        // ReSharper disable once InconsistentNaming
        private readonly MemoryCacheEntryOptions CacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSize(50000) //Size amount
                            //Priority on removing when reaching size limit (memory pressure)
            .SetPriority(CacheItemPriority.High)
            // Remove from cache after this time, regardless of sliding expiration
            .SetAbsoluteExpiration(TimeSpan.FromSeconds(30));


        private MqttClientOptions _options;

        private const string MqttVersionTopic = "$SYS/broker/version";

        public MqttClientOptions Options
        {
            get => _options;
            set
            {
                PrevOptions = _options;
                _options = value;
            }
        }

        private MqttClientOptions PrevOptions
        { get; set; }

        public GlobalOptions GlobalOptions
        {
            get
            {
                return _globalOptions.CurrentValue;
            }
        }

        public MqttClientService(ILogger<MqttClientService> logger, IOptionsMonitor<MqttClientOptions> options,
            IOptionsMonitor<GlobalOptions> globOptions, MqttSubscriptionService subscriptionService)
        {
            _logger = logger;
            _options = options.CurrentValue;
            _globalOptions = globOptions;
            PrevOptions = Options;
            _mqttfactory = new MqttFactory();
            _autoReconnect = false;
            _subscriptionService = subscriptionService;
            _lastHeartbeatTime = DateTime.UtcNow;

            _ = new Timer(MqttHeartbeat, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));

            options.OnChange(OnClientOptionsChanged);

            AsyncHelper.RunSync(ReconnectMqttBrokerAsync);

            _logger.LogDebug("constructor finished");
        }

        private void MqttHeartbeat(object? state)
        {
            if (_mqttClient?.IsConnected == true)
            {
                var topic = GlobalMqttTopic + "/" + $"TS";
                UpdateTimeStampAsync(topic).GetAwaiter().GetResult();
                _lastHeartbeatTime = DateTime.UtcNow;
            }
        }

        public async Task ReconnectMqttBrokerAsync()
        {
            Dispose();

            try
            {
                // This will throw an exception if the broker is not available.
                // The result from this message returns additional data which was sent 
                // from the server. Please refer to the MQTT protocol specification for details.
                _mqttClient = CreateClient(_mqttfactory);
                if (_mqttClient == null)
                {
                    throw new InvalidOperationException("MQTT Client object could not be created");
                }
                _mqttClient.ConnectedAsync += ConnectedAsync;
                _mqttClient.DisconnectedAsync += DisconnectedAsync;
                _mqttClient.ApplicationMessageReceivedAsync += ApplicationMessageReceivedAsync;

                _publishedDataCache.Clear();

                var ret = await ClientConnectAsync(_mqttClient, Options);
#if DEBUG 
                ret.DumpToConsole();
#endif

            }
            catch (MqttCommunicationException e)
            {
                _logger.LogError(e, $"cannot connect to MQTT broker {Options.MqttHost}");
            }

            if (_mqttClient?.IsConnected == true)
            {
                _autoReconnect = GlobalOptions.MqttReconnect;

                if (Options.MqttPort > 0)
                    _logger.LogInformation($"MQTT client connected to {Options.MqttHost}:{Options.MqttPort}");
                else
                    _logger.LogInformation($"MQTT client connected to {Options.MqttHost}");

                await DefineSystemSubscriptionsAsync();
            }
        }

        public async Task<bool> TestAsync(MqttClientOptions connectData)
        {
            var factory = new MqttFactory();

            var client = CreateClient(factory);

            if (client == null)
            {
                throw new InvalidOperationException("MQTT Client object could not be created");
            }

            _ = await this.ClientConnectAsync(client, connectData);

            if (client.IsConnected)
                return true;

            return false;
        }

        private async Task DefineSystemSubscriptionsAsync()
        {
            if (_mqttClient?.IsConnected == true)
            {
                var mqttSubscribeOptions = _mqttfactory.CreateSubscribeOptionsBuilder()
                    .WithTopicFilter(
                        f => { f.WithTopic(MqttVersionTopic); })
                    .Build();

                await _mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
            }
        }

        private Task<bool> CheckHandleSystemSubscriptionsAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic == MqttVersionTopic)
            {
                _brokerVersion = arg.ApplicationMessage.ConvertPayloadToString();
                _logger.LogInformation($"subscription broker version received as '{_brokerVersion}'");

                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        private async Task ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            await arg.AcknowledgeAsync(CancellationToken.None);

            _logger.LogInformation($"subscription message incoming '{arg.ApplicationMessage.Topic}'");
            if (await CheckHandleSystemSubscriptionsAsync(arg))
                // ReSharper disable once RedundantJumpStatement
                return;

            if (await CheckDeviceSubscriberAsync(arg))
                // ReSharper disable once RedundantJumpStatement
                return;
        }

        private async Task<bool> CheckDeviceSubscriberAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            var ret = await _subscriptionService.SubscriberMessageReceivedAsync(arg.ApplicationMessage);

            return ret;

        }

        private IMqttClient? CreateClient(MqttFactory factory)
        {
            var client = factory.CreateMqttClient();

            return client;
        }
        private Task<MqttClientConnectResult> ClientConnectAsync(IMqttClient client, MqttClientOptions localOptions)
        {

            int? port = null;
            if (localOptions.MqttPort > 0) port = localOptions.MqttPort;

            string host = "localhost";
            if (!string.IsNullOrEmpty(localOptions.MqttHost))
            {
                host = localOptions.MqttHost;
            }

            var mqttClientOptionsBuilder = new MqttClientOptionsBuilder().WithTcpServer(host, port);

            if (localOptions.MqttTls)
                mqttClientOptionsBuilder = mqttClientOptionsBuilder.WithTls();
            if (localOptions.MqttV5)
                mqttClientOptionsBuilder = mqttClientOptionsBuilder.WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500);
            if (localOptions.MqttNoFragmentation)
                mqttClientOptionsBuilder = mqttClientOptionsBuilder.WithoutPacketFragmentation();

            if (localOptions.MqttAuthentication) //there is authentication needed
            {
                mqttClientOptionsBuilder =
                    mqttClientOptionsBuilder.WithCredentials(localOptions.MqttUser, localOptions.MqttPassword);
            }

            mqttClientOptionsBuilder = mqttClientOptionsBuilder.WithCleanSession();

            var mqttClientOptions = mqttClientOptionsBuilder.Build();

            return client.ConnectAsync(mqttClientOptions, CancellationToken.None);
        }

        async void OnClientOptionsChanged(MqttClientOptions newOptions, string? s)
        {
            if (!newOptions.Equals(PrevOptions))
            {
                _logger.LogInformation($"MQTT Client Settings changed");
                _logger.LogDebug($"MQTT Client Settings old {PrevOptions} new {Options}");
                Options = newOptions;

                await ReconnectMqttBrokerAsync();

            }

        }

        public event EventHandler<int>? OnConnected;

        private Task ConnectedAsync(MqttClientConnectedEventArgs args)
        {
#if DEBUG
            args.DumpToConsole();
#endif
            _subscriptionService.ClearAll();
            return Task.Run(() =>
            {
                OnConnected?.Invoke(this, 0);
            });
        }

        public event EventHandler<int>? OnDisconnected;
        private async Task DisconnectedAsync(MqttClientDisconnectedEventArgs args)
        {
            _logger.LogInformation("MQTT broker got disconnected");
#if DEBUG
            args.DumpToConsole();
#endif

            _subscriptionService.ClearAll();
            await Task.Run(() =>
            {
                OnDisconnected?.Invoke(this, 0);
            });

            if (_autoReconnect)
            {
                _logger.LogInformation("MQTT establish reconnection");
                await ReconnectMqttBrokerAsync();
            }
        }

        public bool IsConnected
        {
            get
            {
                if (_mqttClient == null) return false;
                return _mqttClient.IsConnected;
            }

        }

        public string BrokerVersion
        {
            get
            {
                if (_mqttClient?.IsConnected == true)
                {
                    return _brokerVersion;
                }

                return "";
            }
        }

        public string Host
        {
            get
            {
                if (Options.MqttPort > 0)
                    return $"{Options.MqttHost}:{Options.MqttPort}";
                else
                    return $"{Options.MqttHost}:1883";
            }
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            if (_mqttClient != null)
            {
                _subscriptionService.ClearAll();

                if (_mqttClient.IsConnected)
                {
                    try
                    {

                        // Send a clean disconnect to the server by calling _DisconnectAsync_. Without this the TCP connection
                        // gets dropped and the server will handle this as a non clean disconnect (see MQTT spec for details).
                        var mqttClientDisconnectOptions = _mqttfactory.CreateClientDisconnectOptionsBuilder().Build();

                        _logger.LogInformation($"clean disconnect MQTT client from {_mqttClient.Options}");
                        AsyncHelper.RunSync(() =>
                            _mqttClient.DisconnectAsync(mqttClientDisconnectOptions, CancellationToken.None));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"disconnecting MQTT client from {_mqttClient.Options}");
                    }
                }

                _mqttClient = null;
            }
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        private string GlobalMqttTopic
        {
            get
            {
                if (string.IsNullOrEmpty(Options.MqttTopic))
                    return "";

                return Options.MqttTopic.Trim(StringHelper.WhiteSpaceCharsPlus(new[] { '/', '\\' })) + "/";
            }
        }

        public TimeSpan DisconnectTime
        {
            get
            {
                if (IsConnected) return TimeSpan.Zero;
                return DateTime.UtcNow - _lastHeartbeatTime;
            }
        }

        public async Task<bool> RemoveSubscriptionAsync(string topic)
        {
            if (_mqttClient?.IsConnected == true)
            {
                try
                {
                    var unsubscribeOptions = new MqttClientUnsubscribeOptionsBuilder().WithTopicFilter(topic).Build();
                    await _mqttClient.UnsubscribeAsync(unsubscribeOptions, CancellationToken.None);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "error unsubscribing");
                    return false;
                }
            }
            else
                _logger.LogWarning("RemoveSubscriptionAsync - broker not connected");

            return false;
        }

        public async Task<bool> AddSubscriptionAsync(string infoTopic)
        {
            if (_mqttClient?.IsConnected == true)
            {
                try
                {
                    var mqttSubscribeOptions = _mqttfactory.CreateSubscribeOptionsBuilder()
                        .WithTopicFilter(
                            f => { f.WithTopic(infoTopic); })
                        .Build();

                    await _mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "error adding subscriber");
                    return false;
                }
            }
            else
                _logger.LogWarning("RemoveSubscriptionAsync - broker not connected");
            return false;
        }
        // ReSharper disable once InconsistentNaming
        public async Task<IEnumerable<Tuple<DP, string>>> PublishAsync(string ID, string name, List<DP> dpList)
        {
            List<Tuple<DP, string>> topics = new List<Tuple<DP, string>>();
            string baseTopic = GlobalMqttTopic + name.Trim();

            if (_mqttClient?.IsConnected == true)
            {
                Dictionary<DP, string> notPublishedErrors = new Dictionary<DP, string>();

                var topic = GlobalMqttTopic + name.Trim() + "/" + $"TS";
                await UpdateTimeStampAsync(topic);

                foreach (var dp in dpList)
                {
                    try
                    {

                        topic = baseTopic + "/" + $"DP{dp.DPNumber}";
                        topics.Add(new Tuple<DP, string>(dp, topic));

                        bool updateBroker = true;

                        if (_publishedDataCache.TryGetValue(topic, out var cachedValue))
                        {
                            if (cachedValue != null)
                            {
                                if ((cachedValue as string) == dp.Value)
                                {
                                    updateBroker = false;
                                }
                            }
                        }

                        if (updateBroker)
                        {
                            _publishedDataCache.Set(topic, dp.Value, CacheEntryOptions);
                            var applicationMessage = new MqttApplicationMessageBuilder()
                                .WithTopic(topic)
                                .WithPayload(dp.Value)
                                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                                .Build();

                            await _mqttClient!.PublishAsync(applicationMessage, CancellationToken.None);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, $"error publishing DP:{dp.DPNumber} with {dp.Value}");
                        notPublishedErrors.Add(dp, ex.Message);
                    }
                }

                if (notPublishedErrors.Count > 0)
                {
                    string message = $"the following DP of {name} could not be published:";
                    foreach (var item in notPublishedErrors)
                    {
                        message += " (" + item.Key.DPNumber + ") " + item.Value;
                    }

                    throw new Exception(message);
                }
            }

            return topics;
        }


        private async Task UpdateTimeStampAsync(string topic)
        {
            if (_mqttClient?.IsConnected == false)
                throw new NullReferenceException("there is no connected MQTT client");

            try
            {
                var timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();

                var applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(timestamp.ToString())
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();

                await _mqttClient!.PublishAsync(applicationMessage, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"error publishing Timestamp for {topic}");
            }
        }
    }


}

