using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using tuya_mqtt.net.Data;
using tuya_mqtt.net.Helper;

namespace tuya_mqtt.net.Services
{
    public class MqttConnectionHealthService : IHealthCheck
    {
        private readonly MqttClientService _mqttService;
        private readonly IOptionsMonitor<GlobalOptions> _globaloptions;
        private GlobalOptions GlobalOptions => _globaloptions.CurrentValue;

        public MqttConnectionHealthService(MqttClientService mqttService, IOptionsMonitor<GlobalOptions> globalIOptions)
        {
            _mqttService = mqttService;
            _globaloptions = globalIOptions;

        }
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                Dictionary<string, object> result = new Dictionary<string, object>();
                if (!_mqttService.IsConnected)
                {
                    result.Add("Disconnect Minutes", _mqttService.DisconnectTime.TotalMinutes);
                    if (_mqttService.DisconnectTime > GlobalOptions.MqttDisconnectUnhealthy)
                    {
                        return Task.FromResult(
                            HealthCheckResult.Unhealthy($"MQTT Disconnected", data: result));
                    }
                    else
                    {
                        return Task.FromResult(
                            HealthCheckResult.Degraded($"MQTT Disconnected", data: result));
                    }
                }

                result.Add("MQTT broker", _mqttService.Host);
                result.Add("MQTT version", _mqttService.BrokerVersion);
                return Task.FromResult(
                        HealthCheckResult.Healthy($"MQTT Connected", data: result));
            }
            catch
            {
                return Task.FromResult(
                    HealthCheckResult.Unhealthy("Error in MqttConnectionHealthService"));
            }

        }
    }
}
