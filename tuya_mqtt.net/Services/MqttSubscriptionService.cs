using MQTTnet;
using System.Collections.Concurrent;
using tuya_mqtt.net.Data;

namespace tuya_mqtt.net.Services
{
    public interface IMqttSubscriptionService
    {
        public Task<bool> SubscriberMessageReceivedAsync(MqttApplicationMessage msg);

        void ClearAll();
    }
    public class MqttSubscriptionService : IMqttSubscriptionService,IDisposable
    {
        private static readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);

        private readonly ILogger _logger;

        private readonly IServiceProvider _serviceProvider;

        private readonly ConcurrentDictionary<SubscriptionKey, SubscriptionInformation> _subscriptions;

        public MqttSubscriptionService(IServiceProvider sp, ILogger<MqttSubscriptionService> logger)
        {
            _serviceProvider = sp;
            _logger = logger;
            _subscriptions = new ConcurrentDictionary<SubscriptionKey, SubscriptionInformation>();
            
        }

       

        public void MakeSubscription(SubscriptionKey subscriptionKey, SubscriptionInformation info)
        {
            
            SemaphoreSlim.Wait();
            try
            {
                bool added;
                if (_subscriptions.TryGetValue(subscriptionKey, out var currentInfo))
                {
                    /* there is already an item */
                    if (currentInfo.Equals(info))
                    {
                        //nothing to do
                    }
                    else
                    {
                        _logger.LogInformation($"Remove MQTT subscription for {subscriptionKey.ID}:{subscriptionKey.ID}");
                        var removed = MqttClient.RemoveSubscriptionAsync(info.Topic).GetAwaiter().GetResult();
                        if (removed)
                        {
                            
                            _subscriptions.Remove(subscriptionKey, out _);

                            added = MqttClient.AddSubscriptionAsync(info.Topic).GetAwaiter().GetResult();
                            if (added)
                                _subscriptions.TryAdd(subscriptionKey, info);
                            else
                                _logger.LogWarning($"Add MQTT subscription did not go through");
                        }
                        else
                            _logger.LogWarning($"Removing MQTT subscription did not go through");
                    }
                }
                else
                {
                    _logger.LogInformation($"Add MQTT subscription for {subscriptionKey.ID}:{subscriptionKey.ID}");
                    added = MqttClient.AddSubscriptionAsync(info.Topic).GetAwaiter().GetResult();
                    if (added)
                        _subscriptions.TryAdd(subscriptionKey, info);
                    else
                        _logger.LogWarning($"Add MQTT subscription did not go through");
                }
            }
            finally { SemaphoreSlim.Release(); }
        }



        private MqttClientService MqttClient
        {
            get
            {
                var service = _serviceProvider.GetRequiredService<MqttClientService>();
                return service;

            }
        }

        public void Dispose()
        {
            
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }


        public async Task<bool> SubscriberMessageReceivedAsync(MqttApplicationMessage applicationMessage)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                var topic = new SubscriptionInformation(applicationMessage.Topic);
                var content = applicationMessage.ConvertPayloadToString();
                foreach (var subscription in _subscriptions)
                {
                    if (subscription.Value.Equals(topic))
                    {
                        _logger.LogInformation(
                            $"subscription received for {subscription.Key.ID}:{subscription.Key.DpNumber}='{content}'");

                        try
                        {
                            await subscription.Value.CallActionAsync(subscription.Key, content);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,"Error handling the subscriber message event");
                        }

                        return true;
                    }
                }

                return false;
            }
            finally
            {
                SemaphoreSlim.Release();
            }
            
        }

        public void ClearAll()
        {
            SemaphoreSlim.Wait();
            try
            {
                _subscriptions.Clear();
            }
            finally { SemaphoreSlim.Release(); }
        }

        public bool IsSubscribed(SubscriptionKey subKey, SubscriptionInformation subInfo)
        {
            SemaphoreSlim.Wait();
            try
            {
                if (_subscriptions.TryGetValue(subKey, out var subscription))
                {
                    if (subscription.Equals(subInfo))
                    {
                        return true;
                    }
                }
                
                return false;
            }
            finally { SemaphoreSlim.Release(); }
        }
    }
}
