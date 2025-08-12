using System;
using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Systems_One_MQTT_Service.Models;

namespace Systems_One_MQTT_Service
{
    /// <summary>
    /// Provides MQTT connectivity and message publishing for the worker service.
    /// </summary>
    public class MqttService : IDisposable
    {
        private readonly ILogger<MqttService> _logger;
        private readonly IMqttClient _mqttClient;
        private readonly MqttClientOptions _mqttClientOptions;
        private readonly MqttConfiguration _mqttConfig;
        private bool _disposed;
        private readonly string _statusTopic;
        private readonly string _statisticsTopic;
        private readonly string _storageTopic;
        private readonly string _clientName;
        private readonly string _location;
        private readonly string _station;
        private readonly string _deviceSerialNumber;
        private int _reconnectAttempts = 0;

        public MqttService(ILogger<MqttService> logger, IConfiguration configuration, string clientName, string location, string station)
        {
            _logger = logger;
            _clientName = clientName;
            _location = location;
            _station = station;
            
            // Get device serial number from configuration
            _deviceSerialNumber = configuration["Device:SerialNumber"] ?? $"{station}-001";
            
            // Load MQTT configuration
            _mqttConfig = new MqttConfiguration();
            configuration.GetSection("MQTT").Bind(_mqttConfig);
            
            // Build topic structure - separate topics for different message types
            var basePath = $"{_mqttConfig.Topics.BaseTopic}/{clientName}/{location}/{station}";
            _statusTopic = $"{basePath}/{_mqttConfig.Topics.StatusSuffix}";
            _statisticsTopic = $"{basePath}/{_mqttConfig.Topics.StatisticsSuffix}";
            _storageTopic = $"{basePath}/{_mqttConfig.Topics.StorageSuffix}";
            
            var mqttFactory = new MqttFactory();
            _mqttClient = mqttFactory.CreateMqttClient();
            
            // Build MQTT client options from configuration
            _mqttClientOptions = BuildMqttClientOptions();
            
            _mqttClient.DisconnectedAsync += HandleDisconnectedAsync;
            
            _logger.LogInformation("MQTT Service initialized with Device Serial Number: {SerialNumber}", _deviceSerialNumber);
            _logger.LogInformation("Status Topic: {StatusTopic}", _statusTopic);
            _logger.LogInformation("Statistics Topic: {StatisticsTopic}", _statisticsTopic);
            _logger.LogInformation("Storage Topic: {StorageTopic}", _storageTopic);
        }

        private MqttClientOptions BuildMqttClientOptions()
        {
            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithClientId($"{_mqttConfig.Client.ClientIdPrefix}_{Guid.NewGuid()}")
                .WithCredentials(_mqttConfig.Authentication.Username, _mqttConfig.Authentication.Password)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(_mqttConfig.Broker.KeepAlive))
                .WithTimeout(TimeSpan.FromMilliseconds(_mqttConfig.Broker.ConnectionTimeout));

            if (_mqttConfig.Client.CleanSession)
            {
                optionsBuilder.WithCleanSession();
            }

            // Configure connection type (WebSocket vs TCP)
            if (_mqttConfig.Broker.UseWebSockets)
            {
                var protocol = _mqttConfig.Broker.UseTLS ? "wss" : "ws";
                var uri = $"{protocol}://{_mqttConfig.Broker.Host}:{_mqttConfig.Broker.Port}{_mqttConfig.Broker.Path}";
                optionsBuilder.WithWebSocketServer(options => { options.Uri = uri; });
            }
            else
            {
                optionsBuilder.WithTcpServer(_mqttConfig.Broker.Host, _mqttConfig.Broker.Port);
            }

            if (_mqttConfig.Broker.UseTLS)
            {
                optionsBuilder.WithTls();
            }

            // Configure Last Will Testament if enabled
            if (_mqttConfig.LastWill.Enabled)
            {
                var lastWillPayload = new
                {
                    device_id = _deviceSerialNumber,
                    ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    device_status = "offline",
                    device_os_version = Environment.OSVersion.ToString()
                };
                var lastWillJson = JsonSerializer.Serialize(lastWillPayload);
                
                optionsBuilder
                    .WithWillTopic(_statusTopic)
                    .WithWillPayload(Encoding.UTF8.GetBytes(lastWillJson))
                    .WithWillQualityOfServiceLevel(ParseQualityOfService(_mqttConfig.LastWill.QualityOfService))
                    .WithWillRetain(_mqttConfig.LastWill.Retain);
                    
                _logger.LogInformation("Last Will Testament configured: {LastWillPayload}", lastWillJson);
            }

            return optionsBuilder.Build();
        }

        private MqttQualityOfServiceLevel ParseQualityOfService(string qosString)
        {
            return qosString?.ToLower() switch
            {
                "atmostonce" => MqttQualityOfServiceLevel.AtMostOnce,
                "atleastonce" => MqttQualityOfServiceLevel.AtLeastOnce,
                "exactlyonce" => MqttQualityOfServiceLevel.ExactlyOnce,
                _ => MqttQualityOfServiceLevel.AtLeastOnce
            };
        }

        private async Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs args)
        {
            _logger.LogWarning("Disconnected from MQTT broker: {Reason}", args.Reason);
            
            if (_reconnectAttempts < _mqttConfig.Client.MaxReconnectAttempts)
            {
                _reconnectAttempts++;
                _logger.LogInformation("Attempting reconnection {Attempt}/{MaxAttempts}", _reconnectAttempts, _mqttConfig.Client.MaxReconnectAttempts);
                
                await Task.Delay(_mqttConfig.Client.ReconnectDelay);
                try 
                { 
                    await ConnectAsync(); 
                    _reconnectAttempts = 0; // Reset on successful connection
                } 
                catch (Exception ex) 
                { 
                    _logger.LogError(ex, "Failed to reconnect to MQTT broker (attempt {Attempt})", _reconnectAttempts); 
                }
            }
            else
            {
                _logger.LogError("Maximum reconnection attempts ({MaxAttempts}) reached. Giving up.", _mqttConfig.Client.MaxReconnectAttempts);
            }
        }

        private async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_mqttClient.IsConnected) return;
            try
            {
                var connectionUri = _mqttConfig.Broker.UseWebSockets 
                    ? $"{(_mqttConfig.Broker.UseTLS ? "wss" : "ws")}://{_mqttConfig.Broker.Host}:{_mqttConfig.Broker.Port}{_mqttConfig.Broker.Path}"
                    : $"{_mqttConfig.Broker.Host}:{_mqttConfig.Broker.Port}";
                    
                _logger.LogInformation("Connecting to MQTT broker at {Uri}...", connectionUri);
                var result = await _mqttClient.ConnectAsync(_mqttClientOptions, cancellationToken);
                if (result.ResultCode == MqttClientConnectResultCode.Success)
                {
                    _logger.LogInformation("Connected successfully to MQTT broker");
                    await PublishStatusMessageAsync("online", cancellationToken);
                }
                else
                {
                    _logger.LogError("Failed to connect to MQTT broker: {ResultCode}", result.ResultCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to MQTT broker");
                throw;
            }
        }

        private async Task PublishStatusMessageAsync(string status, CancellationToken cancellationToken = default)
        {
            try
            {
                var statusPayload = CreateStatusPayload(status);
                var statusJson = JsonSerializer.Serialize(statusPayload);
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(_statusTopic)
                    .WithPayload(Encoding.UTF8.GetBytes(statusJson))
                    .WithQualityOfServiceLevel(ParseQualityOfService(_mqttConfig.Publishing.QualityOfService))
                    .WithRetainFlag(_mqttConfig.Publishing.RetainStatusMessages)
                    .Build();
                await _mqttClient.PublishAsync(message, cancellationToken);
                _logger.LogInformation("Published status message: {Status} for device {DeviceId} to topic {Topic} with retain={Retain}", 
                    status, _deviceSerialNumber, _statusTopic, _mqttConfig.Publishing.RetainStatusMessages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing status message");
            }
        }

        /// <summary>
        /// Creates a Telegraf-friendly status payload
        /// </summary>
        private object CreateStatusPayload(string status)
        {
            return new
            {
                device_id = _deviceSerialNumber,
                ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                device_status = status,
                device_os_version = Environment.OSVersion.ToString()
            };
        }

        /// <summary>
        /// Creates a Telegraf-friendly statistics payload
        /// </summary>
        public object CreateStatisticsPayload(dynamic dbStats)
        {
            return new
            {
                device_id = _deviceSerialNumber,
                ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                statistics = new
                {
                    total_items = (double)dbStats.TotalItems,
                    no_weight = (double)dbStats.NoWeight,
                    good_reads = (double)dbStats.GoodReads,
                    no_reads = (double)dbStats.NoReads,
                    no_dimensions = (double)dbStats.NoDimensions,
                    success = (double)dbStats.Success,
                    out_of_spec = (double)dbStats.OutOfSpec,
                    more_than_one_item = (double)dbStats.MoreThanOneItem,
                    not_sent = (double)dbStats.NotSent,
                    sent = (double)dbStats.Sent
                }
            };
        }

        /// <summary>
        /// Creates a Telegraf-friendly storage payload
        /// </summary>
        public object CreateStoragePayload(IEnumerable<Models.DriveStatistics> driveStats)
        {
            var storage = new Dictionary<string, object>();
            
            foreach (var drive in driveStats.Where(d => d.IsReady))
            {
                var driveLetter = drive.DriveLetter.Replace(":", "").Replace("\\", "");
                storage[driveLetter] = new
                {
                    free_gb = Math.Round(drive.FreeSpaceGB, 2),
                    used_gb = Math.Round(drive.UsedSpaceGB, 2),
                    total_gb = Math.Round(drive.TotalSizeGB, 2),
                    used_pct = Math.Round(drive.PercentageUsed, 2)
                };
            }

            return new
            {
                device_id = _deviceSerialNumber,
                ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                storage = storage
            };
        }

        /// <summary>
        /// Sends statistics data to the MQTT broker using separate topics
        /// </summary>
        public async Task SendStatisticsDataAsync(dynamic dbStats, IEnumerable<Models.DriveStatistics> driveStats, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_mqttClient.IsConnected)
                {
                    await ConnectAsync(cancellationToken);
                }

                if (_mqttClient.IsConnected)
                {
                    // Send statistics payload
                    var statisticsPayload = CreateStatisticsPayload(dbStats);
                    var statisticsJson = JsonSerializer.Serialize(statisticsPayload);
                    var statisticsMessage = new MqttApplicationMessageBuilder()
                        .WithTopic(_statisticsTopic)
                        .WithPayload(Encoding.UTF8.GetBytes(statisticsJson))
                        .WithQualityOfServiceLevel(ParseQualityOfService(_mqttConfig.Publishing.QualityOfService))
                        .WithRetainFlag(_mqttConfig.Publishing.RetainMessages)
                        .Build();
                    await _mqttClient.PublishAsync(statisticsMessage, cancellationToken);
                    _logger.LogInformation("Published statistics data to topic {StatisticsTopic}", _statisticsTopic);

                    // Send storage payload
                    var storagePayload = CreateStoragePayload(driveStats);
                    var storageJson = JsonSerializer.Serialize(storagePayload);
                    var storageMessage = new MqttApplicationMessageBuilder()
                        .WithTopic(_storageTopic)
                        .WithPayload(Encoding.UTF8.GetBytes(storageJson))
                        .WithQualityOfServiceLevel(ParseQualityOfService(_mqttConfig.Publishing.QualityOfService))
                        .WithRetainFlag(_mqttConfig.Publishing.RetainMessages)
                        .Build();
                    await _mqttClient.PublishAsync(storageMessage, cancellationToken);
                    _logger.LogInformation("Published storage data to topic {StorageTopic}", _storageTopic);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending statistics data");
                throw;
            }
        }

        /// <summary>
        /// Connects to the MQTT broker and sends a sample message for testing.
        /// </summary>
        public async Task ConnectAndSendSampleMessageAsync()
        {
            try
            {
                _logger.LogInformation("Starting MQTT connection test");
                
                // Create sample statistics data
                var sampleStatsData = CreateStatisticsPayload(new 
                { 
                    TotalItems = 264, 
                    NoWeight = 4, 
                    GoodReads = 249,
                    NoReads = 15,
                    NoDimensions = 1, 
                    Success = 249,
                    OutOfSpec = 1, 
                    MoreThanOneItem = 10,
                    NotSent = 0, 
                    Sent = 264
                });
                
                // Create sample storage data using the DriveStatistics properties correctly
                var sampleDriveStats = new List<Models.DriveStatistics>
                {
                    new() 
                    { 
                        DriveLetter = "C:", 
                        IsReady = true, 
                        FreeSpaceBytes = (long)(156.8 * 1024 * 1024 * 1024), 
                        UsedSpaceBytes = (long)(297.4 * 1024 * 1024 * 1024), 
                        TotalSizeBytes = (long)(454.2 * 1024 * 1024 * 1024), 
                        PercentageUsed = 65.5 
                    },
                    new() 
                    { 
                        DriveLetter = "E:", 
                        IsReady = true, 
                        FreeSpaceBytes = (long)(14.7 * 1024 * 1024 * 1024), 
                        UsedSpaceBytes = (long)(0.14 * 1024 * 1024 * 1024), 
                        TotalSizeBytes = (long)(14.84 * 1024 * 1024 * 1024), 
                        PercentageUsed = 0.97 
                    }
                };
                var sampleStorageData = CreateStoragePayload(sampleDriveStats);

                var statisticsJson = JsonSerializer.Serialize(sampleStatsData, new JsonSerializerOptions { WriteIndented = true });
                var storageJson = JsonSerializer.Serialize(sampleStorageData, new JsonSerializerOptions { WriteIndented = true });
                
                _logger.LogInformation("Sample Statistics JSON: {StatisticsPayload}", statisticsJson);
                _logger.LogInformation("Sample Storage JSON: {StoragePayload}", storageJson);
                _logger.LogInformation("Connection details - Host: {Host}, Username: {Username}", _mqttConfig.Broker.Host, _mqttConfig.Authentication.Username);
                _logger.LogInformation("Publishing to statistics topic: {StatisticsTopic}", _statisticsTopic);
                _logger.LogInformation("Publishing to storage topic: {StorageTopic}", _storageTopic);

                await ConnectAsync();
                if (_mqttClient.IsConnected)
                {
                    // Send statistics message
                    var statisticsMessage = new MqttApplicationMessageBuilder()
                        .WithTopic(_statisticsTopic)
                        .WithPayload(Encoding.UTF8.GetBytes(statisticsJson))
                        .WithQualityOfServiceLevel(ParseQualityOfService(_mqttConfig.Publishing.QualityOfService))
                        .WithRetainFlag(_mqttConfig.Publishing.RetainMessages)
                        .Build();
                    await _mqttClient.PublishAsync(statisticsMessage);
                    _logger.LogInformation("Sample statistics message published successfully");

                    // Send storage message
                    var storageMessage = new MqttApplicationMessageBuilder()
                        .WithTopic(_storageTopic)
                        .WithPayload(Encoding.UTF8.GetBytes(storageJson))
                        .WithQualityOfServiceLevel(ParseQualityOfService(_mqttConfig.Publishing.QualityOfService))
                        .WithRetainFlag(_mqttConfig.Publishing.RetainMessages)
                        .Build();
                    await _mqttClient.PublishAsync(storageMessage);
                    _logger.LogInformation("Sample storage message published successfully");
                }
                else
                {
                    _logger.LogWarning("Not connected to MQTT broker, messages not sent");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MQTT service");
                throw;
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility - now sends data to separate topics
        /// </summary>
        public async Task SendCustomMessageAsync(object customPayload, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("SendCustomMessageAsync is deprecated. Use SendStatisticsDataAsync for structured data publishing.");
            
            try
            {
                if (!_mqttClient.IsConnected)
                {
                    await ConnectAsync(cancellationToken);
                }
                if (_mqttClient.IsConnected)
                {
                    var jsonPayload = JsonSerializer.Serialize(customPayload, new JsonSerializerOptions { WriteIndented = true });
                    byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);
                    
                    // Send to statistics topic for backward compatibility
                    var applicationMessage = new MqttApplicationMessageBuilder()
                        .WithTopic(_statisticsTopic)
                        .WithPayload(payloadBytes)
                        .WithQualityOfServiceLevel(ParseQualityOfService(_mqttConfig.Publishing.QualityOfService))
                        .WithRetainFlag(_mqttConfig.Publishing.RetainMessages)
                        .Build();
                    await _mqttClient.PublishAsync(applicationMessage, cancellationToken);
                    _logger.LogInformation("Custom message published successfully to statistics topic {StatisticsTopic}", _statisticsTopic);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending custom message");
                throw;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing && _mqttClient != null)
                {
                    try
                    {
                        if (_mqttClient.IsConnected)
                        {
                            PublishStatusMessageAsync("offline").GetAwaiter().GetResult();
                            _mqttClient.DisconnectAsync().GetAwaiter().GetResult();
                            _logger.LogInformation("Disconnected from MQTT broker");
                        }
                        _mqttClient.Dispose();
                        _logger.LogInformation("MQTT client disposed");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing MQTT client");
                    }
                }
                _disposed = true;
            }
        }

        ~MqttService() => Dispose(false);

        // Get the current epoch time in milliseconds
        private static long GetEpochTimeMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
