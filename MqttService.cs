using System;
using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using Microsoft.Extensions.Logging;

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
        private bool _disposed;
        private readonly string _brokerHost = "mqtt.bantryprop.com";
        private readonly string _brokerPath = "/ws";
        private readonly string _username = "Admin";
        private readonly string _password = "Admin";
        private readonly string _topic;
        private readonly string _willTopic;
        private readonly string _clientName;
        private readonly string _location;
        private readonly string _station;

        public MqttService(ILogger<MqttService> logger, string clientName, string location, string station)
        {
            _logger = logger;
            _clientName = clientName;
            _location = location;
            _station = station;
            _topic = $"systems-one/{clientName}/{location}/{station}";
            _willTopic = $"systems-one/{clientName}/{location}/{station}/status";
            var mqttFactory = new MqttFactory();
            _mqttClient = mqttFactory.CreateMqttClient();
            var lastWillPayload = new
            {
                DeviceId = $"{station}-001",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Status = "offline",
                Message = $"Device {station} at {location} has gone offline unexpectedly"
            };
            var lastWillJson = JsonSerializer.Serialize(lastWillPayload);
            _mqttClientOptions = new MqttClientOptionsBuilder()
                .WithWebSocketServer(options => { options.Uri = $"wss://{_brokerHost}{_brokerPath}"; })
                .WithClientId($"SystemsOneMqttClient_{Guid.NewGuid()}")
                .WithCredentials(_username, _password)
                .WithCleanSession()
                .WithTls()
                .WithWillTopic(_willTopic)
                .WithWillPayload(Encoding.UTF8.GetBytes(lastWillJson))
                .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithWillRetain(true)
                .Build();
            _mqttClient.DisconnectedAsync += HandleDisconnectedAsync;
        }

        private async Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs args)
        {
            _logger.LogWarning("Disconnected from MQTT broker: {Reason}", args.Reason);
            await Task.Delay(TimeSpan.FromSeconds(5));
            try { await ConnectAsync(); } catch (Exception ex) { _logger.LogError(ex, "Failed to reconnect to MQTT broker"); }
        }

        private async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_mqttClient.IsConnected) return;
            try
            {
                _logger.LogInformation("Connecting to MQTT broker at wss://{Host}{Path}...", _brokerHost, _brokerPath);
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
                    DeviceId = $"{_station}-001",
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Status = status,
                    Message = $"Device {_station} at {_location} is {status}"
                };
                var statusJson = JsonSerializer.Serialize(statusPayload);
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(_willTopic)
                    .WithPayload(Encoding.UTF8.GetBytes(statusJson))
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag(true)
                    .Build();
                await _mqttClient.PublishAsync(message, cancellationToken);
                _logger.LogInformation("Published status message: {Status}", status);
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
                    DeviceId = "device-001",
                    Timestamp = GetEpochTimeMs(),
                    Statistics = GenerateStatsReadings()
                };
                var jsonPayload = JsonSerializer.Serialize(sampleData, new JsonSerializerOptions { WriteIndented = true });
                byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);
                _logger.LogInformation("Sample JSON message: {payload}", jsonPayload);
                _logger.LogInformation("Connection details - Host: {Host}, Path: {Path}, Username: {Username}", _brokerHost, _brokerPath, _username);
                _logger.LogInformation("Publishing to topic: {Topic}", _topic);
                _logger.LogInformation("Last Will configured for topic: {WillTopic}", _willTopic);
                await ConnectAsync();
                if (_mqttClient.IsConnected)
                {
                    var applicationMessage = new MqttApplicationMessageBuilder()
                        .WithTopic(_topic)
                        .WithPayload(payloadBytes)
                        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                        .WithRetainFlag(false)
                        .Build();
                    _logger.LogInformation("Publishing message to topic {Topic}", _topic);
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
                        .WithTopic(_topic)
                        .WithPayload(payloadBytes)
                        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                        .WithRetainFlag(false)
                        .Build();
                    await _mqttClient.PublishAsync(applicationMessage, cancellationToken);
                    _logger.LogInformation("Custom message published successfully");
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
            new { SensorId = "TotalItems", Value = 264.0, Unit = "count" },
            new { SensorId = "NoWeight", Value = 4.0, Unit = "count" },
            new { SensorId = "Success", Value = 249.0, Unit = "count" },
            new { SensorId = "NoDimensions", Value = 1.0, Unit = "count" },
            new { SensorId = "OutOfSpec", Value = 1.0, Unit = "count" },
            new { SensorId = "NotSent", Value = 0.0, Unit = "count" },
            new { SensorId = "Sent", Value = 249.0, Unit = "count" },
            new { SensorId = "GreaterThanOneItem", Value = 10.0, Unit = "count" }
        };

        // Get the current epoch time in milliseconds
        private static long GetEpochTimeMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
