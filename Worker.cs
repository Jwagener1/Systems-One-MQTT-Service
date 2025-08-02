using System.Text.Json;

namespace Systems_One_MQTT_Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly MqttService _mqttService;
        private int _messageCount = 0;
        private readonly Random _random = new Random();

        public Worker(ILogger<Worker> logger, MqttService mqttService)
        {
            _logger = logger;
            _mqttService = mqttService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait a bit for the initial connection test to complete
            await Task.Delay(5000, stoppingToken);
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                    }
                    
                    // Every 30 seconds, send an MQTT message (but not on every iteration)
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
                    await Task.Delay(5000, stoppingToken); // Wait a bit longer on error
                }
            }
        }
        
        private async Task SendPeriodicUpdateAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Generate a custom message with statistics that change over time
                var payload = CreateDynamicPayload();
                
                // Send the message using the MqttService
                await _mqttService.SendCustomMessageAsync(payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send periodic MQTT message");
            }
        }
        
        private object CreateDynamicPayload()
        {
            // Base values with some randomization
            var totalItems = 260 + _random.Next(0, 20);
            var noWeight = _random.Next(2, 8);
            var outOfSpec = _random.Next(0, 3);
            var noDimensions = _random.Next(0, 3);
            var notSent = _random.Next(0, 2);
            var greaterThanOneItem = _random.Next(8, 15);
            
            // Derived values
            var success = totalItems - noWeight - outOfSpec - noDimensions;
            var sent = success;
            
            // Create statistics array using the Readings format (SensorId, Value, Unit)
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
            
            // Create readings with slightly randomized values
            var readings = new[]
            {
                new { SensorId = "temp-1", Value = Math.Round(22.0 + _random.NextDouble() * 3.0, 1), Unit = "Celsius" },
                new { SensorId = "humidity-1", Value = Math.Round(40.0 + _random.NextDouble() * 10.0, 1), Unit = "%" },
                new { SensorId = "pressure-1", Value = Math.Round(1010.0 + _random.NextDouble() * 10.0, 1), Unit = "hPa" }
            };
            
            // Get epoch time in milliseconds
            var epochTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            // Create the full payload
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
