using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Systems_One_MQTT_Service.Services;

namespace Systems_One_MQTT_Service
{
    /// <summary>
    /// Background worker that queries database every 15 minutes and sends summary statistics to MQTT broker.
    /// </summary>
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly MqttService _mqttService;
        private readonly DataService _dataService;
        private static readonly TimeSpan IntervalDuration = TimeSpan.FromMinutes(15);

        public Worker(ILogger<Worker> logger, MqttService mqttService, DataService dataService)
        {
            _logger = logger;
            _mqttService = mqttService;
            _dataService = dataService;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker started - will send statistics every {Interval} minutes", IntervalDuration.TotalMinutes);
            
            // Wait a bit for the initial connection test to complete
            await Task.Delay(5000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Worker executing at: {time} - querying last 15 minutes of data", DateTimeOffset.Now);
                    
                    // Send 15-minute summary statistics
                    await SendLast15MinutesStatisticsAsync(stoppingToken);
                    
                    // Wait for 15 minutes before next execution
                    _logger.LogInformation("Next statistics update in {Minutes} minutes at {NextTime}", 
                        IntervalDuration.TotalMinutes, 
                        DateTime.Now.Add(IntervalDuration).ToString("yyyy-MM-dd HH:mm:ss"));
                    
                    await Task.Delay(IntervalDuration, stoppingToken);
                }
                catch (Exception ex) when (ex is not TaskCanceledException)
                {
                    _logger.LogError(ex, "Error in worker execution");
                    // Wait 5 minutes before retrying on error
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
        }

        private async Task SendLast15MinutesStatisticsAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Get statistics from the database for the last 15 minutes
                var dbStats = await _dataService.GetLast15MinutesStatisticsAsync();
                
                // Convert database statistics to MQTT payload format
                var payload = CreateStatisticsPayload(dbStats);
                
                // Send the statistics via MQTT
                await _mqttService.SendCustomMessageAsync(payload, cancellationToken);
                
                _logger.LogInformation("Successfully sent 15-minute statistics to MQTT broker");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send 15-minute statistics");
                throw;
            }
        }

        private static object CreateStatisticsPayload(dynamic dbStats)
        {
            // Convert database statistics to the expected MQTT format
            var statistics = new[]
            {
                new { SensorId = "TotalItems", Value = (double)dbStats.TotalItems, Unit = "count" },
                new { SensorId = "NoWeight", Value = (double)dbStats.NoWeight, Unit = "count" },
                new { SensorId = "Success", Value = (double)dbStats.Success, Unit = "count" },
                new { SensorId = "NoDimensions", Value = (double)dbStats.NoDimensions, Unit = "count" },
                new { SensorId = "OutOfSpec", Value = (double)dbStats.OutOfSpec, Unit = "count" },
                new { SensorId = "NotSent", Value = (double)dbStats.NotSent, Unit = "count" },
                new { SensorId = "Sent", Value = (double)dbStats.Sent, Unit = "count" },
                new { SensorId = "Complete", Value = (double)dbStats.Complete, Unit = "count" },
                new { SensorId = "Valid", Value = (double)dbStats.Valid, Unit = "count" },
                new { SensorId = "ImageSent", Value = (double)dbStats.ImageSent, Unit = "count" }
            };

            var epochTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            return new
            {
                DeviceId = "device-001",
                Timestamp = epochTime,
                TimeRange = new
                {
                    StartTime = dbStats.TimeRange.StartTime,
                    EndTime = dbStats.TimeRange.EndTime,
                    DurationMinutes = 15
                },
                Status = "operational",
                Statistics = statistics,
                DataSource = "database_query",
                QueryType = "last_15_minutes"
            };
        }
    }
}
