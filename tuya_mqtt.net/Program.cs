using Awesome.Net.WritableOptions.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using MudBlazor.Services;
using tuya_mqtt.net.Data;
using tuya_mqtt.net.Helper;
using tuya_mqtt.net.Services;

namespace tuya_mqtt.net
{
    public class Program
    {
        
        private const string DataFile = "DataDir/MQTTsettings.json";
        private static ILogger<Program>? _logger;

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Logging.AddInternalLogger();

            // Add services to the container.
            builder.Services.AddRazorPages();
            builder.Services.AddMudServices();
            builder.Services.AddServerSideBlazor();
           
            builder.Services.AddSingleton<TuyaCommunicatorService>();
            builder.Services.AddSingleton<TuyaConnectedDeviceService>();
            builder.Services.AddSingleton<MqttClientService>();
            builder.Services.AddSingleton<MqttSubscriptionService>();
            builder.Services.AddSingleton<LogNotificationService>();

            builder.Services.Configure<TuyaCommunicatorOptions>(
                builder.Configuration.GetSection("Tuya"));
            builder.Services.Configure<GlobalOptions>(
                builder.Configuration.GetSection("Global"));
            builder.Services.Configure<MqttClientOptions>( 
                builder.Configuration.GetSection("MQTT"));
            builder.Services.Configure<LogNotificationServiceOptions>(
                builder.Configuration.GetSection("LogService"));
            builder.Services.Configure<TuyaMonitoredDeviceOptions>(
                builder.Configuration.GetSection("TuyaDev"));

            builder.Configuration.AddJsonFile(DataFile, true);
            builder.Services.ConfigureWritableOptions<MqttClientOptions>(builder.Configuration, "MQTT", DataFile);
            builder.Services.ConfigureWritableOptions<TuyaCommunicatorOptions>(builder.Configuration, "TuyaAPI", DataFile);
            builder.Services.ConfigureWritableOptions<TuyaMonitoredDeviceOptions>(builder.Configuration, "TuyaDevices", DataFile);

            builder.Services.AddHealthChecks()
                .AddCheck<ExceptionCheckHealthService>("exceptions_check")
                .AddCheck<MqttConnectionHealthService>("mqtt_check");

            var app = builder.Build();

            _logger = app.Services.GetService<ILogger<Program>>();
            if (_logger is null)
            {
                Console.WriteLine("no logger instance found");
                throw new NullReferenceException("no logger instance found");
            }

            _logger.LogInformation($"Settings data will be written to {DataFile}");
            CreateDirectoryIfNeeded(app.Environment.ContentRootFileProvider, DataFile);

            using (var scope = app.Services.CreateScope())
            {

                _ = scope.ServiceProvider.GetRequiredService<LogNotificationService>();
                _ = scope.ServiceProvider.GetRequiredService<TuyaCommunicatorService>();
                _ = scope.ServiceProvider.GetRequiredService<MqttClientService>();
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
            }


            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/health", new HealthCheckOptions()
                {
                    ResponseWriter = HealthCheckResponse.WriteResponse,
                    ResultStatusCodes =
                    {
                        [HealthStatus.Healthy] = StatusCodes.Status200OK,
                        [HealthStatus.Degraded] = StatusCodes.Status200OK,
                        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
                    }
                });
            });

            app.MapBlazorHub();
            app.MapFallbackToPage("/_Host");

            app.Run();
        }

        private static void CreateDirectoryIfNeeded(IFileProvider fileProvider, string file)
        {
            _logger?.LogDebug($"CreateDirectoryIfNeeded for '{file}'");
            if (string.IsNullOrEmpty(file)) { throw new ArgumentNullException(nameof(file)); }

            var fileInfo = fileProvider.GetFileInfo(file);
            var filePath = fileInfo.PhysicalPath;
            var dir = Path.GetDirectoryName(filePath);
            _logger?.LogDebug($"check for '{dir}' exists");

            if (!Directory.Exists(dir) && !string.IsNullOrEmpty(dir))
            {
                _logger?.LogInformation($"'{dir}' not exists - creating");
                try
                {
                    Directory.CreateDirectory(dir);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex,$"failed to create directory '{dir}'");
                    throw new Exception($"failed to create directory '{dir}'", ex);
                }
            }
        }
    }
}