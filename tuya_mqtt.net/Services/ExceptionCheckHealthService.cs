using JetBrains.Annotations;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using tuya_mqtt.net.Data;

namespace tuya_mqtt.net.Services
{
    public class ExceptionCheckHealthService : IHealthCheck
    {
        private readonly LogNotificationService _logService;
        private readonly IOptionsMonitor<GlobalOptions> _globaloptions;
        private GlobalOptions GlobalOptions => _globaloptions.CurrentValue;
        public ExceptionCheckHealthService(LogNotificationService logservice, IOptionsMonitor<GlobalOptions> globalIOptions)
        {
            _logService = logservice;
            _globaloptions = globalIOptions;
        }
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                int count = _logService.GetExceptionCountPerMinute();
                Dictionary<string,object> result = new Dictionary<string,object>();
                result.Add("message", $"currently {count} errors/min.");
                result.Add("error/min", count);
                if (count < GlobalOptions.ExceptionsHealthDegraded)
                    return Task.FromResult(
                        HealthCheckResult.Healthy($"Healthy", data: result));
                else
                {
                    if (count >= GlobalOptions.ExceptionsUnhealthy)
                    {
                        return Task.FromResult(
                            HealthCheckResult.Unhealthy($"Unhealthy", data: result));
                    }
                    return Task.FromResult(
                        HealthCheckResult.Degraded($"Degraded", data: result));
                }
            }
            catch
            {
                return Task.FromResult(
                    HealthCheckResult.Unhealthy("Error in ExceptionCheckHealthService"));
            }

        }
    }
}