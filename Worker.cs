using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Systems_One_MQTT_Service.Services;
using System.Globalization;

namespace Systems_One_MQTT_Service
{
    /// <summary>
    /// Background worker that queries database and sends summary statistics to MQTT broker at configurable intervals.
    /// </summary>
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly MqttService _mqttService;
        private readonly DataService _dataService;
        private readonly SystemMonitoringService _systemMonitoringService;
        private readonly IConfiguration _configuration;
        private readonly TimeSpan _intervalDuration;
        private readonly string _deviceSerialNumber;

        public Worker(ILogger<Worker> logger, MqttService mqttService, DataService dataService, SystemMonitoringService systemMonitoringService, IConfiguration configuration)
        {
            _logger = logger;
            _mqttService = mqttService;
            _dataService = dataService;
            _systemMonitoringService = systemMonitoringService;
            _configuration = configuration;
            
            // Get device serial number from configuration
            _deviceSerialNumber = configuration["Device:SerialNumber"] ?? "UNKNOWN-DEVICE-001";
            
            // Get publish interval from configuration (default to 15 minutes) with safe parsing
            var publishIntervalString = configuration["MQTT:Publishing:PublishInterval"] ?? "15";
            if (!int.TryParse(publishIntervalString, NumberStyles.Integer, CultureInfo.InvariantCulture, out var publishIntervalMinutes))
            {
                _logger.LogWarning("Invalid PublishInterval value '{Value}', using default 15 minutes", publishIntervalString);
                publishIntervalMinutes = 15;
            }
            
            _intervalDuration = TimeSpan.FromMinutes(publishIntervalMinutes);
            _logger.LogInformation("Worker configured with {Minutes} minute publish interval", publishIntervalMinutes);
            _logger.LogInformation("Device Serial Number: {SerialNumber}", _deviceSerialNumber);
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker started - will send statistics every {Interval} minutes", _intervalDuration.TotalMinutes);
            
            // Wait a bit for the initial connection test to complete
            await Task.Delay(5000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Worker executing at: {time} - querying last {Minutes} minutes of data and system health", 
                        DateTimeOffset.Now, _intervalDuration.TotalMinutes);
                    
                    // Send statistics including system health
                    await SendStatisticsAsync(stoppingToken);
                    
                    // Wait for configured interval before next execution
                    _logger.LogInformation("Next statistics update in {Minutes} minutes at {NextTime}", 
                        _intervalDuration.TotalMinutes, 
                        DateTime.Now.Add(_intervalDuration).ToString("yyyy-MM-dd HH:mm:ss"));
                    
                    await Task.Delay(_intervalDuration, stoppingToken);
                }
                catch (Exception ex) when (ex is not TaskCanceledException)
                {
                    _logger.LogError(ex, "Error in worker execution");
                    // Wait 5 minutes before retrying on error
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
        }

        private async Task SendStatisticsAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Get statistics from the database for the last interval period
                var dbStats = await _dataService.GetLast15MinutesStatisticsAsync();
                
                // Get system health statistics
                var systemHealth = await _systemMonitoringService.GetSystemHealthStatisticsAsync();
                var driveStats = await _systemMonitoringService.GetDriveStatisticsAsync();
                
                // Send the statistics and storage data via separate MQTT topics
                await _mqttService.SendStatisticsDataAsync(dbStats, driveStats, cancellationToken);
                
                _logger.LogInformation("Successfully sent {Minutes}-minute statistics and storage data to MQTT broker", 
                    _intervalDuration.TotalMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send statistics");
                throw;
            }
        }
    }
}
