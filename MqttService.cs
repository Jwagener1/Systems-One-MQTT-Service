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
        private readonly string _dataTopic;
        private readonly string _statusTopic;
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
            
            // Build topic structure - separate data and status topics
            _dataTopic = $"{_mqttConfig.Topics.BaseTopic}/{clientName}/{location}/{station}/{_mqttConfig.Topics.DataSuffix}";
            _statusTopic = $"{_mqttConfig.Topics.BaseTopic}/{clientName}/{location}/{station}/{_mqttConfig.Topics.StatusSuffix}";
            
            var mqttFactory = new MqttFactory();
            _mqttClient = mqttFactory.CreateMqttClient();
            
            // Build MQTT client options from configuration
            _mqttClientOptions = BuildMqttClientOptions();
            
            _mqttClient.DisconnectedAsync += HandleDisconnectedAsync;
            
            _logger.LogInformation("MQTT Service initialized with Device Serial Number: {SerialNumber}", _deviceSerialNumber);
            _logger.LogInformation("Data Topic: {DataTopic}, Status Topic: {StatusTopic}", _dataTopic, _statusTopic);
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
                    DeviceId = _deviceSerialNumber,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Status = "offline"
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
                var statusPayload = new
                {
                    DeviceId = _deviceSerialNumber,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Status = status
                };
                var statusJson = JsonSerializer.Serialize(statusPayload);
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(_statusTopic)
                    .WithPayload(Encoding.UTF8.GetBytes(statusJson))
                    .WithQualityOfServiceLevel(ParseQualityOfService(_mqttConfig.Publishing.QualityOfService))
                    .WithRetainFlag(_mqttConfig.Publishing.RetainStatusMessages)
                    .Build();
                await _mqttClient.PublishAsync(message, cancellationToken);
                _logger.LogInformation("Published status message: {Status} for device {DeviceId} to topic {Topic}", status, _deviceSerialNumber, _statusTopic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing status message");
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
                var sampleData = new
                {
                    DeviceId = _deviceSerialNumber,
                    Timestamp = GetEpochTimeMs(),
                    Statistics = GenerateStatsReadings()
                };
                var jsonPayload = JsonSerializer.Serialize(sampleData, new JsonSerializerOptions { WriteIndented = true });
                byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);
                _logger.LogInformation("Sample JSON message: {payload}", jsonPayload);
                _logger.LogInformation("Connection details - Host: {Host}, Username: {Username}", _mqttConfig.Broker.Host, _mqttConfig.Authentication.Username);
                _logger.LogInformation("Publishing to data topic: {DataTopic}", _dataTopic);
                _logger.LogInformation("Last Will configured for status topic: {StatusTopic}", _statusTopic);
                await ConnectAsync();
                if (_mqttClient.IsConnected)
                {
                    var applicationMessage = new MqttApplicationMessageBuilder()
                        .WithTopic(_dataTopic)
                        .WithPayload(payloadBytes)
                        .WithQualityOfServiceLevel(ParseQualityOfService(_mqttConfig.Publishing.QualityOfService))
                        .WithRetainFlag(_mqttConfig.Publishing.RetainMessages)
                        .Build();
                    _logger.LogInformation("Publishing message to data topic {DataTopic}", _dataTopic);
                    await _mqttClient.PublishAsync(applicationMessage);
                    _logger.LogInformation("Message published successfully");
                }
                else
                {
                    _logger.LogWarning("Not connected to MQTT broker, message not sent");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MQTT service");
                throw;
            }
        }

        /// <summary>
        /// Sends a custom message to the MQTT broker.
        /// </summary>
        public async Task SendCustomMessageAsync(object customPayload, CancellationToken cancellationToken = default)
        {
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
                    var applicationMessage = new MqttApplicationMessageBuilder()
                        .WithTopic(_dataTopic)
                        .WithPayload(payloadBytes)
                        .WithQualityOfServiceLevel(ParseQualityOfService(_mqttConfig.Publishing.QualityOfService))
                        .WithRetainFlag(_mqttConfig.Publishing.RetainMessages)
                        .Build();
                    await _mqttClient.PublishAsync(applicationMessage, cancellationToken);
                    _logger.LogInformation("Custom message published successfully to data topic {DataTopic}", _dataTopic);
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

        // Generate statistics data using the Readings format (SensorId, Value, Unit)
        private object[] GenerateStatsReadings() => new[]
        {
            // Database statistics
            new { SensorId = "TotalItems", Value = 264.0, Unit = "count" },
            new { SensorId = "NoWeight", Value = 4.0, Unit = "count" },
            new { SensorId = "Success", Value = 249.0, Unit = "count" },
            new { SensorId = "NoDimensions", Value = 1.0, Unit = "count" },
            new { SensorId = "OutOfSpec", Value = 1.0, Unit = "count" },
            new { SensorId = "NotSent", Value = 0.0, Unit = "count" },
            new { SensorId = "Sent", Value = 249.0, Unit = "count" },
            new { SensorId = "GreaterThanOneItem", Value = 10.0, Unit = "count" },
            
            // System monitoring sample data
            new { SensorId = "Drive_C_UsedPercentage", Value = 65.5, Unit = "percent" },
            new { SensorId = "Drive_C_FreeSpaceGB", Value = 156.8, Unit = "GB" },
            new { SensorId = "Drive_C_TotalSpaceGB", Value = 454.2, Unit = "GB" },
            new { SensorId = "Drive_D_UsedPercentage", Value = 12.3, Unit = "percent" },
            new { SensorId = "Drive_D_FreeSpaceGB", Value = 850.1, Unit = "GB" },
            new { SensorId = "Drive_D_TotalSpaceGB", Value = 969.7, Unit = "GB" },
            new { SensorId = "Memory_WorkingSetMB", Value = 125.6, Unit = "MB" },
            new { SensorId = "Memory_ManagedMemoryMB", Value = 45.2, Unit = "MB" }
        };

        // Get the current epoch time in milliseconds
        private static long GetEpochTimeMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
