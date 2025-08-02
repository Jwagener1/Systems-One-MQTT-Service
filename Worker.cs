using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Systems_One_MQTT_Service
{
    /// <summary>
    /// Background worker that periodically sends MQTT messages with dynamic payloads.
    /// </summary>
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly MqttService _mqttService;
        private int _messageCount;
        private static readonly Random Random = new();

        public Worker(ILogger<Worker> logger, MqttService mqttService)
        {
            _logger = logger;
            _mqttService = mqttService;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(5000, stoppingToken); // Allow initial connection test

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                    // Every 30 seconds, send an MQTT message
                    if (_messageCount % 30 == 0)
                    {
                        _logger.LogInformation("Worker sending periodic MQTT message");
                        await SendPeriodicUpdateAsync(stoppingToken);
                    }

                    _messageCount++;
                    await Task.Delay(1000, stoppingToken);
                }
                catch (Exception ex) when (ex is not TaskCanceledException)
                {
                    _logger.LogError(ex, "Error in worker execution");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        private async Task SendPeriodicUpdateAsync(CancellationToken cancellationToken)
        {
            try
            {
                var payload = CreateDynamicPayload();
                await _mqttService.SendCustomMessageAsync(payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send periodic MQTT message");
            }
        }

        private static object CreateDynamicPayload()
        {
            var totalItems = 260 + Random.Next(0, 20);
            var noWeight = Random.Next(2, 8);
            var outOfSpec = Random.Next(0, 3);
            var noDimensions = Random.Next(0, 3);
            var notSent = Random.Next(0, 2);
            var greaterThanOneItem = Random.Next(8, 15);

            var success = totalItems - noWeight - outOfSpec - noDimensions;
            var sent = success;

            var statistics = new[]
            {
                new { SensorId = "TotalItems", Value = (double)totalItems, Unit = "count" },
                new { SensorId = "NoWeight", Value = (double)noWeight, Unit = "count" },
                new { SensorId = "Success", Value = (double)success, Unit = "count" },
                new { SensorId = "NoDimensions", Value = (double)noDimensions, Unit = "count" },
                new { SensorId = "OutOfSpec", Value = (double)outOfSpec, Unit = "count" },
                new { SensorId = "NotSent", Value = (double)notSent, Unit = "count" },
                new { SensorId = "Sent", Value = (double)sent, Unit = "count" },
                new { SensorId = "GreaterThanOneItem", Value = (double)greaterThanOneItem, Unit = "count" }
            };

            var readings = new[]
            {
                new { SensorId = "temp-1", Value = Math.Round(22.0 + Random.NextDouble() * 3.0, 1), Unit = "Celsius" },
                new { SensorId = "humidity-1", Value = Math.Round(40.0 + Random.NextDouble() * 10.0, 1), Unit = "%" },
                new { SensorId = "pressure-1", Value = Math.Round(1010.0 + Random.NextDouble() * 10.0, 1), Unit = "hPa" }
            };

            var epochTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            return new
            {
                DeviceId = "device-001",
                Timestamp = epochTime,
                Readings = readings,
                Status = "operational",
                Statistics = statistics
            };
        }
    }
}
